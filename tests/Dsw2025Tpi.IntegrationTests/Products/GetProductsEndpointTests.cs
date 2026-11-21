using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests.Products;

[Collection("IntegrationTests")]
public class GetProductsEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;

    public GetProductsEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetProducts_PublicEndpoint_ReturnsOkWithPaginatedJsonStructure()
    {
        // Arrange: al menos un producto activo, y el cliente SIN token
        await _factory.SeedProductAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert: 200 y el cuerpo respeta el contrato { productItems, total }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ProductPageDto>();
        page.Should().NotBeNull();
        page!.ProductItems.Should().NotBeEmpty();
        page.Total.Should().BeGreaterThanOrEqualTo(page.ProductItems.Count);
    }

    [Fact]
    public async Task GetProducts_WithQueryStringFilters_ReturnsFilteredResultsFromSqlite()
    {
        // Arrange: 3 productos, 2 contienen el termino buscado
        var term = TestHelpers.Unique("Termino");
        await _factory.SeedProductAsync(name: $"Auricular {term}");
        await _factory.SeedProductAsync(name: $"Parlante {term}");
        await _factory.SeedProductAsync(name: "Webcam sin coincidencia");
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/products?search={term}&pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ProductPageDto>();
        page!.Total.Should().Be(2);
        page.ProductItems.Should().HaveCount(2)
            .And.OnlyContain(p => p.Name.Contains(term));
    }

    [Fact]
    public async Task GetAuthProducts_AdminEndpoint_WhenCalledByAdmin_IncludesInactiveProducts()
    {
        // Arrange: un producto inactivo con un nombre unico
        var term = TestHelpers.Unique("Inactivo");
        var inactive = await _factory.SeedProductAsync(name: $"Producto {term}", isActive: false);
        var client = _factory.CreateClient();

        // El endpoint publico NO lo muestra (sin resultados = 204)
        var publicResponse = await client.GetAsync($"/api/products?search={term}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: el endpoint de admin SI
        client.UseToken(await TestHelpers.GetAdminTokenAsync(client));
        var response = await client.GetAsync($"/api/products/admin?search={term}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ProductPageDto>();
        page!.ProductItems.Should().ContainSingle(p => p.Id == inactive.Id && !p.IsActive);
    }
}