using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dsw2025Tpi.UnitTests.Orders;

public class CreateOrderTests
{
    private readonly Mock<IRepository> _repository = new();
    private readonly OrdersManagementService _service;
    private readonly Customer _customer = new() { Name = "Cliente Test", Email = "cliente@test.com" };

    public CreateOrderTests()
    {
        _service = new OrdersManagementService(_repository.Object, NullLogger<OrdersManagementService>.Instance);
    }

    private void CustomerExists() =>
        _repository.Setup(r => r.GetById<Customer>(_customer.Id, It.IsAny<string[]>())).ReturnsAsync(_customer);

    private void ProductExists(Product product) =>
        _repository.Setup(r => r.GetById<Product>(product.Id, It.IsAny<string[]>())).ReturnsAsync(product);

    private OrderRequest NewOrder(params OrderItemRequest[] items) =>
        new(_customer.Id, "Calle Falsa 123", "Calle Falsa 123", null, items.ToList());

    [Fact]
    public async Task CreateOrder_WithValidItemsAndStock_CalculatesTotalAndDecrementsProductStock()
    {
        var mouse = new Product("SKU-M", "Mouse", 1500m, 10, null, "INT-M");
        var teclado = new Product("SKU-T", "Teclado", 4000m, 5, null, "INT-T");
        CustomerExists();
        ProductExists(mouse);
        ProductExists(teclado);
        _repository.Setup(r => r.Update(It.IsAny<Product>())).ReturnsAsync((Product p) => p);
        _repository.Setup(r => r.Add(It.IsAny<Order>())).ReturnsAsync((Order o) => o);

        var result = await _service.CreateOrder(NewOrder(new OrderItemRequest(mouse.Id, 2), new OrderItemRequest(teclado.Id, 1)));

        result.TotalAmount.Should().Be(7000m);
        result.TotalAmount.Should().Be(result.Items.Sum(i => i.Subtotal));
        mouse.StockQuantity.Should().Be(8);
        teclado.StockQuantity.Should().Be(4);
        _repository.Verify(r => r.Update(mouse), Times.Once);
        _repository.Verify(r => r.Update(teclado), Times.Once);
        _repository.Verify(r => r.Add(It.Is<Order>(o => o.TotalAmount == 7000m && o.OrderItems!.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
    {
        var producto = new Product("SKU-S", "Auricular", 2000m, 2, null, "INT-S");
        CustomerExists();
        ProductExists(producto);

        var act = () => _service.CreateOrder(NewOrder(new OrderItemRequest(producto.Id, 5)));

        await act.Should().ThrowAsync<InsufficientStockException>();
        producto.StockQuantity.Should().Be(2);
        _repository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
        _repository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrder_WhenCustomerDoesNotExist_ThrowsCustomerNotFoundException()
    {
        _repository.Setup(r => r.GetById<Customer>(It.IsAny<Guid>(), It.IsAny<string[]>()))
                   .ReturnsAsync((Customer?)null);

        var act = () => _service.CreateOrder(NewOrder(new OrderItemRequest(Guid.NewGuid(), 1)));

        await act.Should().ThrowAsync<CustomerNotFoundException>();
        _repository.Verify(r => r.GetById<Product>(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
        _repository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
        _repository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }
}