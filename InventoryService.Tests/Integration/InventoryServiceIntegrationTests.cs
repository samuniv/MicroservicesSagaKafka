using Microsoft.EntityFrameworkCore;
using InventoryService.Domain.Entities;
using InventoryService.Infrastructure.Data;
using InventoryService.Infrastructure.MessageBus;
using InventoryService.Infrastructure.Repositories;
using InventoryService.Events.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace InventoryService.Tests.Integration;

public class InventoryServiceIntegrationTests : IDisposable
{
    private readonly InventoryDbContext _dbContext;
    private readonly InventoryRepository _repository;
    private readonly Mock<KafkaProducerService> _mockKafkaProducer;
    private readonly Mock<ILogger<InventoryService.Services.InventoryService>> _mockLogger;
    private readonly Services.InventoryService _service;

    public InventoryServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new InventoryDbContext(options);
        _repository = new InventoryRepository(_dbContext);
        _mockKafkaProducer = new Mock<KafkaProducerService>(Mock.Of<ILogger<KafkaProducerService>>());
        _mockLogger = new Mock<ILogger<Services.InventoryService>>();
        _service = new Services.InventoryService(_repository, _mockKafkaProducer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ReserveStock_WithSufficientQuantity_ShouldSucceed()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventoryItem = new InventoryItem(productId, 100);
        await _repository.CreateAsync(inventoryItem);

        // Act
        await _service.ReserveStockAsync(productId, 30, orderId);

        // Assert
        var updatedItem = await _repository.GetByProductIdAsync(productId);
        updatedItem.Should().NotBeNull();
        updatedItem!.Reserved.Should().Be(30);
        updatedItem.Available.Should().Be(70);

        _mockKafkaProducer.Verify(x => x.PublishInventoryEventAsync(
            It.Is<InventoryReservedEvent>(e => 
                e.OrderId == orderId && 
                e.ProductId == productId)),
            Times.Once);
    }

    [Fact]
    public async Task ReserveStock_WithInsufficientQuantity_ShouldPublishFailureEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventoryItem = new InventoryItem(productId, 20);
        await _repository.CreateAsync(inventoryItem);

        // Act
        await _service.ReserveStockAsync(productId, 30, orderId);

        // Assert
        var updatedItem = await _repository.GetByProductIdAsync(productId);
        updatedItem.Should().NotBeNull();
        updatedItem!.Reserved.Should().Be(0);
        updatedItem.Available.Should().Be(20);

        _mockKafkaProducer.Verify(x => x.PublishInventoryEventAsync(
            It.Is<InventoryReservationFailedEvent>(e => 
                e.OrderId == orderId && 
                e.ProductId == productId)),
            Times.Once);
    }

    [Fact]
    public async Task ReleaseStock_ShouldUpdateInventoryAndPublishEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var inventoryItem = new InventoryItem(productId, 100);
        await _repository.CreateAsync(inventoryItem);
        await _service.ReserveStockAsync(productId, 30, orderId);

        // Act
        await _service.ReleaseStockAsync(productId, 20, orderId);

        // Assert
        var updatedItem = await _repository.GetByProductIdAsync(productId);
        updatedItem.Should().NotBeNull();
        updatedItem!.Reserved.Should().Be(10);
        updatedItem.Available.Should().Be(90);

        _mockKafkaProducer.Verify(x => x.PublishInventoryEventAsync(
            It.Is<InventoryReleasedEvent>(e => 
                e.OrderId == orderId && 
                e.ProductId == productId)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStock_ShouldUpdateQuantityAndPublishEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var inventoryItem = new InventoryItem(productId, 100);
        await _repository.CreateAsync(inventoryItem);

        // Act
        await _service.UpdateStockAsync(productId, 150);

        // Assert
        var updatedItem = await _repository.GetByProductIdAsync(productId);
        updatedItem.Should().NotBeNull();
        updatedItem!.Quantity.Should().Be(150);
        updatedItem.Available.Should().Be(150);

        _mockKafkaProducer.Verify(x => x.PublishInventoryEventAsync(
            It.Is<StockUpdatedEvent>(e => 
                e.ProductId == productId && 
                e.NewQuantity == 150)),
            Times.Once);
    }

    [Fact]
    public async Task GetLowStockItems_ShouldReturnItemsBelowThreshold()
    {
        // Arrange
        var threshold = 50;
        await _repository.CreateAsync(new InventoryItem(Guid.NewGuid(), 30));
        await _repository.CreateAsync(new InventoryItem(Guid.NewGuid(), 40));
        await _repository.CreateAsync(new InventoryItem(Guid.NewGuid(), 100));

        // Act
        var lowStockItems = await _service.GetLowStockItemsAsync(threshold);

        // Assert
        lowStockItems.Should().HaveCount(2);
        lowStockItems.Should().AllSatisfy(item => item.Quantity.Should().BeLessThan(threshold));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
} 