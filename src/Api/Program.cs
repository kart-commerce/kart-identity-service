using Kart.Identity.Api.Endpoints;
using Kart.Identity.Application;
using Kart.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// api-contract.yaml: every versioned business endpoint starts at /v1
// (kart-conventions.md, "API Versioning"); /.well-known/jwks.json is the one
// deliberate exception (IANA well-known discovery path convention).
app.MapJwksEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program;
