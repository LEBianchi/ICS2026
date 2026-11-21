using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests.E2E;

// E2E a nivel de API: el flujo completo solo con requests HTTP, sin tocar la base desde la prueba.
[Collection("IntegrationTests")]
public class PurchaseFlowE2ETests
{
    private readonly CustomWebApplicationFactory _factory;

    public PurchaseFlowE2ETests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PurchaseFlow_AdminCreatesProduct_CustomerBuys_StockIsUpdated()
    {
        var admin = _factory.CreateClient();
        var customer = _factory.CreateClient();
        var sku = TestHelpers.Unique("E2E");

        // 1-2. El admin inicia sesion y crea un producto con 5 unidades
        admin.UseToken(await TestHelpers.GetAdminTokenAsync(admin));
        var created = await admin.PostAsJsonAsync("/api/products", new
        {
            sku,
            internalCode = TestHelpers.Unique("INT"),
            name = $"Producto {sku}",
            description = "Flujo E2E",
            currentUnitPrice = 1200m,
            stockQuantity = 5
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var productId = (await created.Content.ReadFromJsonAsync<ProductItemDto>())!.Id;

        // 3. Un cliente se registra (la API le crea su Customer)
        var username = TestHelpers.Unique("comprador");
        var register = await customer.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            password = "Cliente123!$",
            email = $"{username}@test.local"
        });
        register.EnsureSuccessStatusCode();
        using var registerBody = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        customer.UseToken(registerBody.RootElement.GetProperty("token").GetString()!);
        var customerId = registerBody.RootElement.GetProperty("customerId").GetGuid();

        // 4. El producto aparece en el catalogo publico
        var catalog = await customer.GetFromJsonAsync<ProductPageDto>($"/api/products?search={sku}");
        catalog!.ProductItems.Should().ContainSingle(p => p.Id == productId);

        // 5. El cliente compra 2 unidades
        var order = await customer.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            shippingAddress = "San Martin 1000",
            billingAddress = "San Martin 1000",
            notes = (string?)null,
            items = new[] { new { productId, quantity = 2 } }
        });
        order.StatusCode.Should().Be(HttpStatusCode.Created);

        // 6. La orden se puede consultar por el Location que devolvio la API
        var fetched = await customer.GetAsync(order.Headers.Location);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. El stock publicado bajo de 5 a 3
        var product = await customer.GetFromJsonAsync<ProductItemDto>($"/api/products/{productId}");
        product!.StockQuantity.Should().Be(3);
    }
}
