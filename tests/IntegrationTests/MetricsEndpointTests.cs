using System.Net;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// observability-standards.md's mandatory Prometheus scrape target — RED metrics
/// (rate/errors/duration) exposed via the OpenTelemetry Prometheus exporter.
/// </summary>
public class MetricsEndpointTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task GetMetrics_ReturnsPrometheusExpositionFormat()
    {
        var client = factory.CreateClient();

        // A prior request so the ASP.NET Core instrumentation has at least one
        // http.server.request.duration measurement recorded before the scrape.
        await client.GetAsync("/.well-known/jwks.json");

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("http_server_request_duration_seconds", body);
    }
}
