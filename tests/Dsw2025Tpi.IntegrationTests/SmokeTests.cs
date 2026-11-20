using System.Net;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests;

// Prueba de humo: la API arranca con SQLite (tablas, roles y admin creados) y responde.
[Collection("IntegrationTests")]
public class SmokeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Api_Starts_And_HealthcheckReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthcheck");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
