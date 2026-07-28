using Kart.Identity.Api.Endpoints;
using Kart.Identity.Api.HealthChecks;
using Kart.Identity.Api.Middleware;
using Kart.Identity.Application;
using Kart.Identity.Infrastructure;
using Kart.Shared.Configuration;
using Kart.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig();

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
builder.AddKartObservability("kart-identity-service");

// AddOpenTelemetry()/WithMetrics() are additive across calls - this composes with the MeterProvider
// AddKartObservability already registered (same Prometheus/OTLP exporters) rather than replacing it,
// so OutboxRelayHostedService's custom meter is scraped without touching Kart.Shared.Observability.
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter("Kart.Identity.OutboxRelay"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// /health/live: process is up, no dependency check. /health/ready: this service's job depends
// on Postgres being reachable AND migrated (a connectable-but-unmigrated database, e.g. tonight's
// missing outbox_events table, is not "ready") - matching kart-infra's service-chart probe
// convention (search-service's OpenSearchHealthCheck is the same pattern for its own data store).
builder.Services.AddHealthChecks()
    .AddCheck<IdentityDbHealthCheck>("identity-db", tags: ["ready"]);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

// Per-HTTP-request Information log (method/path/status/elapsed) — the RED-style
// access log observability-standards.md expects on every endpoint, for free.
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseMiddleware<UserContextEnrichmentMiddleware>();
app.UseAuthorization();

// Prometheus scrape target (observability-standards.md's mandatory `/metrics`).
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// api-contract.yaml: every versioned business endpoint starts at /v1
// (kart-conventions.md, "API Versioning"); /.well-known/jwks.json is the one
// deliberate exception (IANA well-known discovery path convention).
app.MapJwksEndpoints();
app.MapAuthEndpoints();
app.MapMfaEndpoints();
app.MapInternalUserEndpoints();
app.MapEnterpriseSsoEndpoints();
app.MapSocialSsoEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program;
