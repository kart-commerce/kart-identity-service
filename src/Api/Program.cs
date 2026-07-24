using Kart.Identity.Api.Endpoints;
using Kart.Identity.Api.Middleware;
using Kart.Identity.Application;
using Kart.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

// api-contract.yaml: every versioned business endpoint starts at /v1
// (kart-conventions.md, "API Versioning"); /.well-known/jwks.json is the one
// deliberate exception (IANA well-known discovery path convention).
app.MapJwksEndpoints();
app.MapAuthEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program;
