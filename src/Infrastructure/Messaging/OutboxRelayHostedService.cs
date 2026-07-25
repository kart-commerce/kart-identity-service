using System.Text;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Relays `outbox_events` rows (OutboxEvent.cs) to `identity.exchange`
/// (design-decisions.md, "Event Publication Reliability"; message-bus-manifest.json —
/// topic, durable). Declares the exchange idempotently at startup. Connects lazily
/// with its own retry loop, mirroring kart-category-service's OutboxRelayHostedService,
/// so a RabbitMQ outage at boot degrades publish latency for UserRegistered/
/// SessionCreated/UserAccountUpdated, never crashes the Api process.
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);

                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Identity outbox relay lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunRelayLoopAsync(IModel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingBatchAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayPendingBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var pending = await dbContext.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var outboxEvent in pending)
        {
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.EventId.ToString();
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: _options.Exchange,
                routingKey: RoutingKeyFor(outboxEvent.EventType),
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(DateTimeOffset.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>event-contract.md's "Routing Key" column for this service's three published events.</summary>
    private static string RoutingKeyFor(string eventType) => eventType switch
    {
        "UserRegistered" => "identity.user.registered",
        "SessionCreated" => "identity.session.created",
        "UserAccountUpdated" => "identity.user-account.updated",
        _ => throw new InvalidOperationException($"No routing key mapping for outbox event type '{eventType}'."),
    };
}
