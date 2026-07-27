using Kart.Identity.Api.Endpoints;
using Kart.Identity.Api.Middleware;
using Kart.Identity.Api.Observability;
using Kart.Identity.Application;
using Kart.Identity.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
