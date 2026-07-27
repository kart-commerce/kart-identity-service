using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Declares this service's full <see cref="MessageBusManifest"/> topology once at startup,
/// registered ahead of <see cref="OutboxRelayHostedService"/> and
/// <see cref="UserDataErasedConsumerHostedService"/> (the generic host runs
/// <see cref="IHostedService.StartAsync"/> in registration order) so the topology exists
/// before either publishes or consumes anything. A RabbitMQ outage at boot must not crash
/// the process — same degrade-not-crash contract as the other two hosted services — so
/// failure here just logs; each of them re-declares the same manifest idempotently inside
/// its own retrying connection anyway.
/// </summary>
public sealed class RabbitMqTopologyStartupHostedService : IHostedService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<RabbitMqTopologyStartupHostedService> _logger;

    public RabbitMqTopologyStartupHostedService(
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<RabbitMqTopologyStartupHostedService> logger)
    {
        _connectionFactory = connectionFactory;
        _manifest = manifest;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();
            RabbitMqTopologyProvisioner.Declare(channel, _manifest);
            _logger.LogInformation(
                "Declared RabbitMQ topology for {Service} from message-bus manifest ({ExchangeCount} exchange(s), {QueueCount} queue(s)).",
                _manifest.Service,
                _manifest.Exchanges.Count + _manifest.ExternalExchanges.Count,
                _manifest.Queues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not declare RabbitMQ topology for {Service} at startup; the publisher/consumer " +
                "hosted services will retry this themselves once RabbitMQ is reachable.",
                _manifest.Service);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
