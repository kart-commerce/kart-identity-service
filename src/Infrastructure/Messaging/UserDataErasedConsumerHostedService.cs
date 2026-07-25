using System.Text;
using System.Text.Json;
using Kart.Identity.Application.Features.ConsumeUserDataErased;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Consumes `UserDataErased` off `identity.user-events.queue` (bound to User Service's own
/// `user.exchange` / `user.data-erased` — this service does not own that exchange,
/// message-bus-manifest.json) and dispatches <see cref="ConsumeUserDataErasedCommand"/>
/// via MediatR, resolved through a scoped <see cref="IServiceScopeFactory"/> exactly like
/// <see cref="OutboxRelayHostedService"/> resolves its own scoped DbContext.
/// </summary>
/// <remarks>
/// event-contract.md's retry tier for `UserDataErased` is "5x, exponential backoff, on-call
/// paging on exhaustion" (compliance-critical — ADR-0016) — a stricter policy than
/// message-bus-manifest.json's single `identity.user-events.retry.30s` scaffold literally
/// shows. This host generalizes that scaffold into a 5-tier exponential ladder
/// (30s/60s/120s/240s/480s), all named `identity.user-events.retry.&lt;Ns&gt;` per the same
/// convention, each still dead-lettering back into `identity.user-events.queue` on TTL
/// expiry exactly as the manifest's single tier does. A custom `x-identity-retry-count`
/// header (not RabbitMQ's own `x-death` bookkeeping, which is harder to reason about across
/// several distinct queues) tracks how many of the 5 tiers a message has already been
/// through; only once it has passed through all 5 does a failure land the message in the
/// terminal `identity.user-events.dlq` (via the main queue's own configured DLX), at which
/// point this host logs Critical — the on-call-paging hook the contract calls for.
/// RabbitMQ's own ack/nack-then-requeue machinery is the only retry mechanism here; no
/// duplicate in-process retry counter is kept anywhere else.
/// </remarks>
public sealed class UserDataErasedConsumerHostedService : BackgroundService
{
    private const string QueueName = "identity.user-events.queue";
    private const string DlqName = "identity.user-events.dlq";
    private const string RoutingKey = "user.data-erased";
    private const string RetryCountHeader = "x-identity-retry-count";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConnectionPollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 5x exponential backoff (event-contract.md) — tier N's TTL is the delay before a
    /// message that failed for the Nth time is redelivered to the main queue.
    /// </summary>
    private static readonly (string QueueName, int TtlMilliseconds)[] RetryTiers =
    [
        ("identity.user-events.retry.30s", 30_000),
        ("identity.user-events.retry.60s", 60_000),
        ("identity.user-events.retry.120s", 120_000),
        ("identity.user-events.retry.240s", 240_000),
        ("identity.user-events.retry.480s", 480_000),
    ];

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<UserDataErasedConsumerHostedService> _logger;

    public UserDataErasedConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<UserDataErasedConsumerHostedService> logger)
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
                DeclareTopology(channel);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += (_, ea) => HandleDeliveryAsync(channel, ea, stoppingToken);
                channel.BasicConsume(QueueName, autoAck: false, consumer);

                await WaitWhileConnectedAsync(connection, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserDataErased consumer lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private static async Task WaitWhileConnectedAsync(IConnection connection, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
        {
            await Task.Delay(ConnectionPollInterval, stoppingToken);
        }
    }

    /// <summary>
    /// message-bus-manifest.json's topology, generalized per event-contract.md (see remarks
    /// on the type) — `identity.dlx` and `user.exchange` (declared idempotently; Identity
    /// only binds a queue to the latter, it does not own it), the main consumer queue and
    /// its DLQ, and the 5-tier retry ladder.
    /// </summary>
    private void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(_options.DeadLetterExchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(_options.UserExchange, ExchangeType.Topic, durable: true);

        channel.QueueDeclare(DlqName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(DlqName, _options.DeadLetterExchange, routingKey: DlqName);

        foreach (var (retryQueueName, ttlMilliseconds) in RetryTiers)
        {
            var retryArgs = new Dictionary<string, object>
            {
                ["x-message-ttl"] = ttlMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = QueueName,
            };
            channel.QueueDeclare(retryQueueName, durable: true, exclusive: false, autoDelete: false, arguments: retryArgs);
        }

        var mainQueueArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DlqName,
        };
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: mainQueueArgs);
        channel.QueueBind(QueueName, _options.UserExchange, routingKey: RoutingKey);

        channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
    }

    private async Task HandleDeliveryAsync(IModel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<UserDataErasedEventPayload>(delivery.Body.Span, PayloadSerializerOptions)
                ?? throw new InvalidOperationException("UserDataErased message body deserialized to null.");

            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ConsumeUserDataErasedCommand(payload.UserId, payload.ErasedAt), stoppingToken);

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process UserDataErased delivery (tag {DeliveryTag})", delivery.DeliveryTag);
            RouteToNextRetryTierOrDlq(channel, delivery);
        }
    }

    private void RouteToNextRetryTierOrDlq(IModel channel, BasicDeliverEventArgs delivery)
    {
        var attempt = GetRetryCount(delivery.BasicProperties) + 1;

        if (attempt > RetryTiers.Length)
        {
            _logger.LogCritical(
                "UserDataErased exhausted all {MaxAttempts} retry attempts (delivery tag {DeliveryTag}) — routing to {Dlq}. " +
                "Compliance-critical tier (event-contract.md, ADR-0016): this requires on-call paging.",
                RetryTiers.Length,
                delivery.DeliveryTag,
                DlqName);
            channel.BasicReject(delivery.DeliveryTag, requeue: false);
            return;
        }

        var (retryQueueName, _) = RetryTiers[attempt - 1];
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = delivery.BasicProperties.ContentType;
        properties.MessageId = delivery.BasicProperties.MessageId;
        properties.Headers = new Dictionary<string, object> { [RetryCountHeader] = attempt };

        channel.BasicPublish(exchange: string.Empty, routingKey: retryQueueName, basicProperties: properties, body: delivery.Body);
        channel.BasicAck(delivery.DeliveryTag, multiple: false);
    }

    private static int GetRetryCount(IBasicProperties properties)
    {
        if (properties.Headers is not null && properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                _ => 0,
            };
        }

        return 0;
    }
}
