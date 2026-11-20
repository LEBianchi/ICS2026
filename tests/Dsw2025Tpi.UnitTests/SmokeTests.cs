using Dsw2025Tpi.Domain.Entities;
using FluentAssertions;

namespace Dsw2025Tpi.UnitTests;

// Prueba de humo: confirma que el proyecto compila, referencia al dominio y xUnit la descubre.
public class SmokeTests
{
    [Fact]
    public void NewProduct_IsActiveByDefault()
    {
        var product = new Product("SKU-1", "Producto", 100m, 1);

        product.IsActive.Should().BeFalse();
    }
}
