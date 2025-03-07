using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.MessageBus;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.Saga;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using OrderService.Events.IntegrationEvents;

namespace OrderService.Tests.Integration;

public class OrderServiceIntegrationTests : IDisposable
{
    private readonly OrderDbContext _dbContext;
    private readonly OrderRepository _orderRepository;
    private readonly Mock<KafkaProducerService> _mockKafkaProducer;
    private readonly Mock<ILogger<OrderCreationSaga>> _mockLogger;
    private readonly OrderCreationSaga _saga;

    public OrderServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new OrderDbContext(options);
        _orderRepository = new OrderRepository(_dbContext);
        _mockKafkaProducer = new Mock<KafkaProducerService>(Mock.Of<ILogger<KafkaProducerService>>());
        _mockLogger = new Mock<ILogger<OrderCreationSaga>>();
        _saga = new OrderCreationSaga(_orderRepository, _mockKafkaProducer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateOrder_ShouldStartSagaAndRequestInventoryReservation()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 2, 10.00m);
        await _orderRepository.CreateAsync(order);

        // Act
        await _saga.StartAsync(order);

        // Assert
        _mockKafkaProducer.Verify(x => x.PublishOrderEventAsync(
            It.Is<RequestInventoryReservationEvent>(e => 
                e.OrderId == order.Id && 
                e.Items.Count == 1)), 
            Times.Once);
    }

    [Fact]
    public async Task HandleInventoryReservedEvent_ShouldUpdateOrderStatusAndRequestPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 2, 10.00m);
        await _orderRepository.CreateAsync(order);

        var reservedEvent = new InventoryReservedEvent(
            order.Id,
            order.CustomerId.ToString(),
            order.TotalAmount,
            order.Items.Select(i => new OrderItemEvent 
            { 
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price,
                Subtotal = i.Subtotal
            }).ToList());

        // Act
        await _saga.HandleInventoryReservedEvent(reservedEvent);

        // Assert
        var updatedOrder = await _orderRepository.GetByIdAsync(order.Id);
        updatedOrder.Status.Should().Be(OrderStatus.InventoryReserved);

        _mockKafkaProducer.Verify(x => x.PublishOrderEventAsync(
            It.Is<RequestPaymentProcessingEvent>(e => 
                e.OrderId == order.Id && 
                e.TotalAmount == order.TotalAmount)), 
            Times.Once);
    }

    [Fact]
    public async Task HandlePaymentCompletedEvent_ShouldCompleteOrder()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 2, 10.00m);
        order.UpdateStatus(OrderStatus.InventoryReserved);
        await _orderRepository.CreateAsync(order);

        var paymentCompletedEvent = new PaymentCompletedEvent(
            order.Id,
            order.CustomerId.ToString(),
            order.TotalAmount,
            "transaction-123");

        // Act
        await _saga.HandlePaymentCompletedEvent(paymentCompletedEvent);

        // Assert
        var completedOrder = await _orderRepository.GetByIdAsync(order.Id);
        completedOrder.Status.Should().Be(OrderStatus.Completed);

        _mockKafkaProducer.Verify(x => x.PublishOrderEventAsync(
            It.Is<OrderCompletedIntegrationEvent>(e => 
                e.OrderId == order.Id)), 
            Times.Once);
    }

    [Fact]
    public async Task HandlePaymentFailedEvent_ShouldFailOrderAndReleaseInventory()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 2, 10.00m);
        order.UpdateStatus(OrderStatus.InventoryReserved);
        await _orderRepository.CreateAsync(order);

        var paymentFailedEvent = new PaymentFailedEvent(
            order.Id,
            order.CustomerId.ToString(),
            order.TotalAmount,
            "Payment declined",
            "CreditCard");

        // Act
        await _saga.HandlePaymentFailedEvent(paymentFailedEvent);

        // Assert
        var failedOrder = await _orderRepository.GetByIdAsync(order.Id);
        failedOrder.Status.Should().Be(OrderStatus.Failed);

        _mockKafkaProducer.Verify(x => x.PublishOrderEventAsync(
            It.Is<ReleaseInventoryEvent>(e => 
                e.OrderId == order.Id)), 
            Times.Once);

        _mockKafkaProducer.Verify(x => x.PublishOrderEventAsync(
            It.Is<OrderFailedIntegrationEvent>(e => 
                e.OrderId == order.Id && 
                e.FailureReason == paymentFailedEvent.FailureReason)), 
            Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
} 