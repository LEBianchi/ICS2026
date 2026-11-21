using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dsw2025Tpi.UnitTests.Products;

public class AddProductTests
{
    private readonly Mock<IRepository> _repository = new();
    private readonly ProductsManagementService _service;

    public AddProductTests()
    {
        _service = new ProductsManagementService(_repository.Object, NullLogger<ProductsManagementService>.Instance);
    }

    [Fact]
    public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
    {
        // Arrange: no hay otro producto con ese SKU y Add devuelve lo que recibe
        var request = new ProductModel.Request("SKU-001", "INT-001", "Mouse Gamer", "RGB", 15000m, 20);
        _repository.Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                   .ReturnsAsync((Product?)null);
        _repository.Setup(r => r.Add(It.IsAny<Product>()))
                   .ReturnsAsync((Product p) => p);

        // Act
        var result = await _service.AddProduct(request);

        // Assert
        result.Should().NotBeNull();
        result.Sku.Should().Be("SKU-001");
        result.Name.Should().Be("Mouse Gamer");
        result.CurrentUnitPrice.Should().Be(15000m);
        result.StockQuantity.Should().Be(20);
        result.IsActive.Should().BeTrue();
        _repository.Verify(r => r.Add(It.Is<Product>(p => p.Sku == "SKU-001")), Times.Once);
    }

    [Fact]
    public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
    {
        // Arrange: el repositorio "encuentra" un producto con el mismo SKU
        var request = new ProductModel.Request("SKU-001", "INT-002", "Teclado", null, 5000m, 5);
        _repository.Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                   .ReturnsAsync(new Product("SKU-001", "Existente", 100m, 1));

        // Act
        var act = () => _service.AddProduct(request);

        // Assert
        await act.Should().ThrowAsync<DuplicatedEntityException>();
        _repository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }

    [Theory]
    [InlineData(100, 0)]   // stock en cero
    [InlineData(100, -3)]  // stock negativo
    [InlineData(-1, 10)]   // precio negativo
    public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(decimal price, int stock)
    {
        // Arrange
        var request = new ProductModel.Request("SKU-002", "INT-003", "Monitor", null, price, stock);

        // Act
        var act = () => _service.AddProduct(request);

        // Assert: falla con mensaje y antes de tocar el repositorio
        var error = await act.Should().ThrowAsync<InvalidDataException>();
        error.Which.Message.Should().NotBeNullOrWhiteSpace();
        _repository.Verify(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()), Times.Never);
        _repository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }
}
