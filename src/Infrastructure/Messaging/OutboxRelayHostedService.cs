using System.Diagnostics.Metrics;
using System.Text;
using Kart.Identity.Infrastructure.Persistence;
using Kart.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Relays `outbox_events` rows (OutboxEvent.cs) to whichever exchange/routing key
/// message-bus-manifest.json's `publishedEvents` maps each event type to
/// (design-decisions.md, "Event Publication Reliability"). Declares the full manifest
/// topology idempotently on every (re)connect via <see cref="RabbitMqTopologyProvisioner"/>.
/// Connects lazily with its own retry loop, mirroring kart-category-service's
/// OutboxRelayHostedService, so a RabbitMQ outage at boot degrades publish latency for
/// UserRegistered/SessionCreated/UserAccountUpdated, never crashes the Api process.
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    // A stuck relay (e.g. tonight's missing outbox_events table) previously only showed up as
    // scrolling ERROR logs, with no distinction from an ordinary transient RabbitMQ blip. These
    // two gauges make that distinction alertable: ConsecutiveFailures climbing without bound, or
    // OldestPendingEventAgeSeconds growing past this service's normal poll cadence, both mean
    // events aren't draining and a human should look, not just "reconnecting in 10s" forever.
    private static readonly Meter Meter = new("Kart.Identity.OutboxRelay");
    private static long _consecutiveFailures;
    private static double _oldestPendingEventAgeSeconds;

    private static readonly Counter<long> RelayFailureCounter = Meter.CreateCounter<long>(
        "identity_outbox_relay_failures_total",
        description: "Count of outbox relay loop failures (RabbitMQ connection or database query), each triggering a reconnect/retry.");

    private static readonly ObservableGauge<long> ConsecutiveFailureGauge = Meter.CreateObservableGauge(
        "identity_outbox_relay_consecutive_failures",
        () => Interlocked.Read(ref _consecutiveFailures),
        description: "Consecutive relay failures since the last successful poll. A sustained non-zero value means the relay is stuck (e.g. schema drift, broker outage) rather than recovering.");

    private static readonly ObservableGauge<double> OldestPendingEventAgeGauge = Meter.CreateObservableGauge(
        "identity_outbox_oldest_pending_event_age_seconds",
        () => Volatile.Read(ref _oldestPendingEventAgeSeconds),
        unit: "s",
        description: "Age of the oldest unpublished outbox_events row as of the last successful poll (0 when the queue is empty). A growing value means events aren't draining.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _manifest = manifest;
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
                RabbitMqTopologyProvisioner.Declare(channel, _manifest);

                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RelayFailureCounter.Add(1);
                Interlocked.Increment(ref _consecutiveFailures);

                // Not necessarily a RabbitMQ problem - this also catches RelayPendingBatchAsync's
                // own database query failing (e.g. a missing/unmigrated outbox_events table),
                // which used to be misreported here as a broker connection loss.
                _logger.LogError(ex, "Identity outbox relay failed; reconnecting in {Delay}.", ReconnectDelay);
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

        // The query above succeeded, so the relay is no longer stuck even if nothing is pending.
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        _oldestPendingEventAgeSeconds = pending.Count == 0
            ? 0
            : (DateTimeOffset.UtcNow - pending[0].OccurredAt).TotalSeconds;

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
                exchange: _manifest.ExchangeFor(outboxEvent.EventType),
                routingKey: _manifest.RoutingKeyFor(outboxEvent.EventType),
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(DateTimeOffset.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
