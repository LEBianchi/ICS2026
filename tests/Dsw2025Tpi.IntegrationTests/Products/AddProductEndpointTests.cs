using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.IntegrationTests.Products;

[Collection("IntegrationTests")]
public class AddProductEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AddProductEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static object NewProductPayload(string sku) => new
    {
        sku,
        internalCode = TestHelpers.Unique("INT"),
        name = "Notebook de prueba",
        description = "Creada por la prueba de integracion",
        currentUnitPrice = 850000m,
        stockQuantity = 4
    };

    [Fact]
    public async Task AddProduct_WhenUnauthenticatedOrNotAdmin_ReturnsUnauthorizedOrForbidden()
    {
        var client = _factory.CreateClient();
        var payload = NewProductPayload(TestHelpers.Unique("SKU"));

        // Sin token: 401
        var anonymous = await client.PostAsJsonAsync("/api/products", payload);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Con token de cliente (rol USER): 403
        client.UseToken(await TestHelpers.GetUserTokenAsync(client));
        var notAdmin = await client.PostAsJsonAsync("/api/products", payload);
        notAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddProduct_WhenAuthenticatedAsAdmin_PersistsInSqliteAndReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.UseToken(await TestHelpers.GetAdminTokenAsync(client));
        var sku = TestHelpers.Unique("SKU");

        // Act
        var response = await client.PostAsJsonAsync("/api/products", NewProductPayload(sku));

        // Assert: 201 y la fila esta en la tabla Products
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await _factory.WithDbAsync(async db =>
        {
            var saved = await db.Set<Product>().AsNoTracking().SingleAsync(p => p.Sku == sku);
            saved.Name.Should().Be("Notebook de prueba");
            saved.CurrentUnitPrice.Should().Be(850000m);
            saved.StockQuantity.Should().Be(4);
            saved.IsActive.Should().BeTrue();
        });
    }

    [Fact]
    public async Task AddProduct_WhenSkuAlreadyExistsInDatabase_ReturnsConflictStatusCode()
    {
        // Arrange: el SKU ya esta en la base
        var existing = await _factory.SeedProductAsync();
        var client = _factory.CreateClient();
        client.UseToken(await TestHelpers.GetAdminTokenAsync(client));

        // Act
        var response = await client.PostAsJsonAsync("/api/products", NewProductPayload(existing.Sku!));

        // Assert: 409 (no un 500) y no se duplico la fila
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.WithDbAsync(async db =>
            (await db.Set<Product>().CountAsync(p => p.Sku == existing.Sku)).Should().Be(1));
    }
}
