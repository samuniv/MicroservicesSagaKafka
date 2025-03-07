using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace OrderService.Tests.Domain;

public class OrderTests
{
    [Fact]
    public void CreateOrder_WithValidData_ShouldSucceed()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Act
        var order = new Order(customerId);

        // Assert
        order.Should().NotBeNull();
        order.Id.Should().NotBe(Guid.Empty);
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be(OrderStatus.Created);
        order.TotalAmount.Should().Be(0);
        order.Items.Should().BeEmpty();
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddItem_WhenOrderIsCreated_ShouldSucceed()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        var productId = Guid.NewGuid();
        var quantity = 2;
        var price = 10.00m;

        // Act
        order.AddItem(productId, quantity, price);

        // Assert
        order.Items.Should().HaveCount(1);
        var item = order.Items.First();
        item.ProductId.Should().Be(productId);
        item.Quantity.Should().Be(quantity);
        item.Price.Should().Be(price);
        item.Subtotal.Should().Be(quantity * price);
        order.TotalAmount.Should().Be(quantity * price);
    }

    [Fact]
    public void AddItem_WhenOrderIsNotInCreatedState_ShouldThrowException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        order.UpdateStatus(OrderStatus.InventoryReserved);

        // Act & Assert
        var act = () => order.AddItem(Guid.NewGuid(), 1, 10.00m);
        act.Should().Throw<InvalidOrderStateException>()
            .WithMessage("Cannot modify items after order is processed");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_WithInvalidQuantity_ShouldThrowException(int invalidQuantity)
    {
        // Arrange
        var order = new Order(Guid.NewGuid());

        // Act & Assert
        var act = () => order.AddItem(Guid.NewGuid(), invalidQuantity, 10.00m);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Quantity must be greater than zero*");
    }

    [Fact]
    public void UpdateStatus_WithValidTransition_ShouldSucceed()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());

        // Act
        order.UpdateStatus(OrderStatus.InventoryReserved);

        // Assert
        order.Status.Should().Be(OrderStatus.InventoryReserved);
        order.UpdatedAt.Should().NotBeNull();
        order.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateStatus_WithInvalidTransition_ShouldThrowException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());

        // Act & Assert
        var act = () => order.UpdateStatus(OrderStatus.Completed);
        act.Should().Throw<InvalidOrderStateException>()
            .WithMessage("Invalid status transition from Created to Completed");
    }

    [Fact]
    public void RemoveItem_WhenItemExists_ShouldSucceed()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        var productId = Guid.NewGuid();
        order.AddItem(productId, 1, 10.00m);

        // Act
        order.RemoveItem(productId);

        // Assert
        order.Items.Should().BeEmpty();
        order.TotalAmount.Should().Be(0);
    }

    [Fact]
    public void RemoveItem_WhenItemDoesNotExist_ShouldThrowException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        var nonExistentProductId = Guid.NewGuid();

        // Act & Assert
        var act = () => order.RemoveItem(nonExistentProductId);
        act.Should().Throw<ArgumentException>()
            .WithMessage($"Item with ProductId {nonExistentProductId} not found in order");
    }

    [Fact]
    public void ValidateState_WithValidOrder_ShouldSucceed()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 1, 10.00m);

        // Act & Assert
        var act = () => order.ValidateState();
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateState_WithNoItems_ShouldThrowException()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());

        // Act & Assert
        var act = () => order.ValidateState();
        act.Should().Throw<InvalidOrderStateException>()
            .WithMessage("Order must have at least one item");
    }
} 