using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace Dsw2025Tpi.UnitTests.Products;

public class GetProductsTests
{
    private readonly Mock<IRepository> _repository = new();
    private readonly ProductsManagementService _service;

    public GetProductsTests()
    {
        _service = new ProductsManagementService(
            _repository.Object,
            NullLogger<ProductsManagementService>.Instance);
    }

    private void RepositoryReturns(List<Product> products)
    {
        _repository.Setup(r => r.GetFiltered(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                products.AsQueryable().Where(predicate).ToList());
    }

    private static Product NewProduct(
        string name,
        bool isActive = true,
        string? sku = null,
        string? internalCode = null)
        => new(sku ?? $"SKU-{name}", name, 1000m, 10, null, internalCode ?? $"INT-{name}")
        {
            IsActive = isActive
        };

    [Fact]
    public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
    {
        // Arrange: lista mixta
        RepositoryReturns(new List<Product>
        {
            NewProduct("Activo A"),
            NewProduct("Inactivo B", isActive: false),
            NewProduct("Activo C"),
            NewProduct("Inactivo D", isActive: false)
        });

        var filter = new ProductModel.FilterProduct(null, null, null, null);

        // Act
        var result = await _service.GetProducts(filter, includeInactive: false);

        // Assert
        result.ProductItems.Should().HaveCount(2);
        result.ProductItems.Should().OnlyContain(p => p.IsActive);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
    {
        // Arrange: 15 productos "Producto 01".."Producto 15" (el servicio ordena por nombre)
        RepositoryReturns(Enumerable.Range(1, 15)
            .Select(i => NewProduct($"Producto {i:00}"))
            .ToList());

        var filter = new ProductModel.FilterProduct(null, null, PageNumber: 2, PageSize: 5);

        // Act
        var result = await _service.GetProducts(filter);

        // Assert: la segunda pagina son los productos 06 a 10, y el total sigue siendo 15
        result.ProductItems.Should().HaveCount(5);
        result.ProductItems.Select(p => p.Name).Should().Equal(
            "Producto 06", "Producto 07", "Producto 08", "Producto 09", "Producto 10");
        result.Total.Should().Be(15);
    }

    [Fact]
    public async Task GetProducts_WithSearchTerm_FiltersByNameSkuOrInternalCode()
    {
        // Arrange: el termino aparece en el nombre de uno, en el SKU de otro y en el codigo interno de otro
        RepositoryReturns(new List<Product>
        {
            NewProduct("Mouse Gamer", sku: "PER-001", internalCode: "A-1"),
            NewProduct("Teclado Mecanico", sku: "PER-002", internalCode: "A-2"),
            NewProduct("Monitor", sku: "PER-003", internalCode: "A-3"),
            NewProduct("Pad XL", sku: "MOUSE-PAD-01", internalCode: "A-4"),
            NewProduct("Combo", sku: "PER-005", internalCode: "COMBO-MOUSE")
        });

        var filter = new ProductModel.FilterProduct(null, "mouse", null, null);

        // Act
        var result = await _service.GetProducts(filter);

        result.ProductItems.Select(p => p.Name).Should().BeEquivalentTo(new[] { "Mouse Gamer", "Pad XL", "Combo" });
        result.Total.Should().Be(3);
    }
}