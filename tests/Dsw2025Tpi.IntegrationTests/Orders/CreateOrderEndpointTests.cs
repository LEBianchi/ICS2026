using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.IntegrationTests.Orders;

[Collection("IntegrationTests")]
public class CreateOrderEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CreateOrderEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static object NewOrderPayload(Guid customerId, Guid productId, int quantity) => new
    {
        customerId,
        shippingAddress = "Av. Siempre Viva 742",
        billingAddress = "Av. Siempre Viva 742",
        notes = "Orden de prueba",
        items = new[] { new { productId, quantity } }
    };

    [Fact]
    public async Task CreateOrder_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders",
            NewOrderPayload(Guid.NewGuid(), Guid.NewGuid(), 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrder_WhenAuthenticatedWithValidData_ReturnsCreatedAndPersistsOrderAndStockInSqlite()
    {
        // Arrange: 1) token valido 2) cliente y producto con stock 10 en SQLite
        var client = _factory.CreateClient();
        client.UseToken(await TestHelpers.GetUserTokenAsync(client));
        var customer = await _factory.SeedCustomerAsync();
        var product = await _factory.SeedProductAsync(stock: 10, price: 2500m);

        // Act: 3) comprar 3 unidades
        var response = await client.PostAsJsonAsync("/api/orders",
            NewOrderPayload(customer.Id, product.Id, 3));

        // Assert: 4) 201 con Location
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = body.RootElement.GetProperty("orderId").GetGuid();

        // 5) la orden esta en Orders y el stock bajo a 7 en Products
        await _factory.WithDbAsync(async db =>
        {
            var order = await db.Set<Order>().AsNoTracking().SingleAsync(o => o.Id == orderId);
            order.CustomerId.Should().Be(customer.Id);
            order.TotalAmount.Should().Be(7500m);

            var stored = await db.Set<Product>().AsNoTracking().SingleAsync(p => p.Id == product.Id);
            stored.StockQuantity.Should().Be(7);
        });
    }

    [Fact]
    public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequestWithErrorMessage()
    {
        // Arrange: hay 2 unidades y se piden 5
        var client = _factory.CreateClient();
        client.UseToken(await TestHelpers.GetUserTokenAsync(client));
        var customer = await _factory.SeedCustomerAsync();
        var product = await _factory.SeedProductAsync(stock: 2);

        // Act
        var response = await client.PostAsJsonAsync("/api/orders",
            NewOrderPayload(customer.Id, product.Id, 5));

        // Assert: 400 con un JSON que explica la falta de stock, y el stock no cambio
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        error.RootElement.GetProperty("Message").GetString()
            .Should().Contain("no tiene suficiente stock");

        await _factory.WithDbAsync(async db =>
            (await db.Set<Product>().AsNoTracking().SingleAsync(p => p.Id == product.Id))
                .StockQuantity.Should().Be(2));
    }
}