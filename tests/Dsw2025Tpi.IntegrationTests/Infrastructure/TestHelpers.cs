using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

// Lo que devuelve GET /api/products (solo los campos que usan las pruebas)
public record ProductItemDto(Guid Id, string Sku, string Name, int StockQuantity, bool IsActive);
public record ProductPageDto(List<ProductItemDto> ProductItems, int Total);

public static class TestHelpers
{
    // Valor unico para que las pruebas no choquen entre si dentro de la misma base
    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    // Token del administrador sembrado por Program.cs (SeedAdmin__*)
    public static async Task<string> GetAdminTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = CustomWebApplicationFactory.AdminUsername,
            password = CustomWebApplicationFactory.AdminPassword
        });
        response.EnsureSuccessStatusCode();
        return await ReadTokenAsync(response);
    }

    // Token de un cliente comun (rol USER), registrado en el momento
    public static async Task<string> GetUserTokenAsync(HttpClient client)
    {
        var username = Unique("cliente");
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            password = "Cliente123!$",
            email = $"{username}@test.local"
        });
        response.EnsureSuccessStatusCode();
        return await ReadTokenAsync(response);
    }

    public static void UseToken(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // Inserta un producto directo en SQLite (sin pasar por la API)
    public static async Task<Product> SeedProductAsync(this CustomWebApplicationFactory factory,
        string? name = null, int stock = 10, decimal price = 1000m, bool isActive = true, string? sku = null)
    {
        var product = new Product(sku ?? Unique("SKU"), name ?? Unique("Producto"), price, stock, null, Unique("INT"))
        {
            IsActive = isActive
        };
        await factory.WithDbAsync(async db =>
        {
            db.Add(product);
            await db.SaveChangesAsync();
        });
        return product;
    }

    public static async Task<Customer> SeedCustomerAsync(this CustomWebApplicationFactory factory)
    {
        var name = Unique("cli");
        var customer = new Customer { Name = name, Email = $"{name}@test.local" };
        await factory.WithDbAsync(async db =>
        {
            db.Add(customer);
            await db.SaveChangesAsync();
        });
        return customer;
    }

    // Abre un DbContext nuevo sobre la misma base SQLite que usa la API
    public static async Task WithDbAsync(this CustomWebApplicationFactory factory, Func<Dsw2025TpiContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        await action(db);
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }
}
