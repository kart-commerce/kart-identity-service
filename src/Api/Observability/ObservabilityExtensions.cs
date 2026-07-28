using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

namespace Kart.Identity.Api.Observability;

/// <summary>
/// observability-standards.md's mandated stack (Serilog -> Loki, OpenTelemetry -> Tempo/
/// Prometheus), wired locally in this composition root — kart-conventions.md's
/// `Kart.Shared.Observability` doesn't exist as a published package yet (kart-shared has
/// no source beyond its README), so this is shaped to be a drop-in replacement once it does.
/// </summary>
public static class ObservabilityExtensions
{
    public const string ServiceName = "kart-identity-service";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration["Observability:Otlp:Endpoint"];
        const string consoleTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}";

        // Console sink emits structured JSON; shipping to Loki is the OTel Collector's
        // job (OTLP log exporter), never something this process does directly. In
        // Development there's no collector tailing stdout, so a human wants to read
        // it directly — use a plain templated console instead of compact JSON there.
        //
        // The file sink is a local convenience only (kept outside the repo next to
        // GlobalConfig, per PLATFORM_BLUEPRINT.md Configuration Management) — it's
        // opt-in via Observability:LogFile:Directory and unset in any environment
        // that already gets its logs from the collector.
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .Enrich.WithProperty("service", ServiceName);

            if (context.HostingEnvironment.IsDevelopment())
            {
                loggerConfiguration.WriteTo.Console(outputTemplate: consoleTemplate);
            }
            else
            {
                loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
            }

            var logFileDirectory = context.Configuration["Observability:LogFile:Directory"];
            if (!string.IsNullOrWhiteSpace(logFileDirectory))
            {
                var logFilePath = Path.Combine(logFileDirectory, $"{ServiceName}-.log");

                if (context.HostingEnvironment.IsDevelopment())
                {
                    loggerConfiguration.WriteTo.File(
                        logFilePath,
                        outputTemplate: consoleTemplate,
                        rollingInterval: RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10 * 1024 * 1024);
                }
                else
                {
                    loggerConfiguration.WriteTo.File(
                        new CompactJsonFormatter(),
                        logFilePath,
                        rollingInterval: RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10 * 1024 * 1024);
                }
            }
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                // RED metrics (rate/errors/duration) on every HTTP endpoint, per
                // observability-standards.md — ASP.NET Core's own instrumentation
                // already emits http.server.request.duration; scraped at /metrics.
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return builder;
    }
}
