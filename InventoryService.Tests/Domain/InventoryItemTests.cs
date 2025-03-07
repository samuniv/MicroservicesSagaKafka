using InventoryService.Domain.Entities;
using InventoryService.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace InventoryService.Tests.Domain;

public class InventoryItemTests
{
    [Fact]
    public void CreateInventoryItem_WithValidData_ShouldSucceed()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var initialQuantity = 100;

        // Act
        var inventoryItem = new InventoryItem(productId, initialQuantity);

        // Assert
        inventoryItem.Should().NotBeNull();
        inventoryItem.Id.Should().NotBe(Guid.Empty);
        inventoryItem.ProductId.Should().Be(productId);
        inventoryItem.Quantity.Should().Be(initialQuantity);
        inventoryItem.Reserved.Should().Be(0);
        inventoryItem.Available.Should().Be(initialQuantity);
        inventoryItem.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateInventoryItem_WithInvalidQuantity_ShouldThrowException(int invalidQuantity)
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act & Assert
        var act = () => new InventoryItem(productId, invalidQuantity);
        act.Should().Throw<InvalidInventoryOperationException>()
            .WithMessage("Initial quantity must be greater than zero");
    }

    [Fact]
    public void ReserveStock_WithSufficientQuantity_ShouldSucceed()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        var reservationQuantity = 30;

        // Act
        inventoryItem.ReserveStock(reservationQuantity);

        // Assert
        inventoryItem.Reserved.Should().Be(reservationQuantity);
        inventoryItem.Available.Should().Be(70);
        inventoryItem.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReserveStock_WithInsufficientQuantity_ShouldThrowException()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 20);
        var reservationQuantity = 30;

        // Act & Assert
        var act = () => inventoryItem.ReserveStock(reservationQuantity);
        act.Should().Throw<InsufficientStockException>()
            .WithMessage($"Insufficient available stock. Requested: {reservationQuantity}, Available: {inventoryItem.Available}");
    }

    [Fact]
    public void ReleaseStock_WithValidQuantity_ShouldSucceed()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        inventoryItem.ReserveStock(30);

        // Act
        inventoryItem.ReleaseStock(20);

        // Assert
        inventoryItem.Reserved.Should().Be(10);
        inventoryItem.Available.Should().Be(90);
        inventoryItem.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReleaseStock_MoreThanReserved_ShouldThrowException()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        inventoryItem.ReserveStock(20);

        // Act & Assert
        var act = () => inventoryItem.ReleaseStock(30);
        act.Should().Throw<InvalidInventoryOperationException>()
            .WithMessage("Cannot release more stock than is reserved");
    }

    [Fact]
    public void UpdateStock_WithValidQuantity_ShouldSucceed()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        var newQuantity = 150;

        // Act
        inventoryItem.UpdateStock(newQuantity);

        // Assert
        inventoryItem.Quantity.Should().Be(newQuantity);
        inventoryItem.Available.Should().Be(newQuantity);
        inventoryItem.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateStock_LessThanReserved_ShouldThrowException()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        inventoryItem.ReserveStock(30);

        // Act & Assert
        var act = () => inventoryItem.UpdateStock(20);
        act.Should().Throw<InvalidInventoryOperationException>()
            .WithMessage("New stock level cannot be less than reserved quantity");
    }

    [Fact]
    public void CommitReservation_WithValidQuantity_ShouldSucceed()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        inventoryItem.ReserveStock(30);

        // Act
        inventoryItem.CommitReservation(20);

        // Assert
        inventoryItem.Quantity.Should().Be(80);
        inventoryItem.Reserved.Should().Be(10);
        inventoryItem.Available.Should().Be(70);
        inventoryItem.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CommitReservation_MoreThanReserved_ShouldThrowException()
    {
        // Arrange
        var inventoryItem = new InventoryItem(Guid.NewGuid(), 100);
        inventoryItem.ReserveStock(20);

        // Act & Assert
        var act = () => inventoryItem.CommitReservation(30);
        act.Should().Throw<InvalidInventoryOperationException>()
            .WithMessage("Cannot commit more items than are reserved");
    }
} 