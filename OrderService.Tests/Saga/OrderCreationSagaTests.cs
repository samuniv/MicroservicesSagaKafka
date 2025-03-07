using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using OrderService.Events.IntegrationEvents;
using OrderService.Infrastructure.MessageBus;
using OrderService.Infrastructure.Saga;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace OrderService.Tests.Saga;

public class OrderCreationSagaTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<KafkaProducerService> _mockKafkaProducer;
    private readonly Mock<ILogger<OrderCreationSaga>> _mockLogger;
    private readonly OrderCreationSaga _saga;

    public OrderCreationSagaTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockKafkaProducer = new Mock<KafkaProducerService>(Mock.Of<ILogger<KafkaProducerService>>());
        _mockLogger = new Mock<ILogger<OrderCreationSaga>>();
        _saga = new OrderCreationSaga(
            _mockOrderRepository.Object,
            _mockKafkaProducer.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CompleteSagaFlow_Success()
    {
        // Arrange
        var order = CreateTestOrder();
        _mockOrderRepository.Setup(r => r.GetByIdAsync(order.Id))
            .ReturnsAsync(order);

        // Act & Assert - Step 1: Start Saga
        await _saga.StartAsync(order);
        _mockKafkaProducer.Verify(p => p.PublishOrderEventAsync(
            It.Is<RequestInventoryReservationEvent>(e => e.OrderId == order.Id)),
            Times.Once);

        // Act & Assert - Step 2: Handle Inventory Reserved
        var inventoryReservedEvent = new InventoryReservedEvent(
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

        await _saga.HandleInventoryReservedEvent(inventoryReservedEvent);
        order.Status.Should().Be(OrderStatus.InventoryReserved);
        _mockKafkaProducer.Verify(p => p.PublishOrderEventAsync(
            It.Is<RequestPaymentProcessingEvent>(e => e.OrderId == order.Id)),
            Times.Once);

        // Act & Assert - Step 3: Handle Payment Completed
        var paymentCompletedEvent = new PaymentCompletedEvent(
            order.Id,
            order.CustomerId.ToString(),
            order.TotalAmount,
            "transaction-123");

        await _saga.HandlePaymentCompletedEvent(paymentCompletedEvent);
        order.Status.Should().Be(OrderStatus.Completed);
        _mockKafkaProducer.Verify(p => p.PublishOrderEventAsync(
            It.Is<OrderCompletedIntegrationEvent>(e => 
                e.OrderId == order.Id)),
            Times.Once);
    }

    [Fact]
    public async Task SagaFlow_InventoryReservationFailure()
    {
        // Arrange
        var order = CreateTestOrder();
        _mockOrderRepository.Setup(r => r.GetByIdAsync(order.Id))
            .ReturnsAsync(order);

        // Act - Start Saga and Simulate Inventory Failure
        await _saga.StartAsync(order);
        await _saga.HandleInventoryReservationFailedEvent(new InventoryReservationFailedEvent(
            order.Id,
            order.CustomerId.ToString(),
            order.TotalAmount,
            "Insufficient stock"));

        // Assert
        order.Status.Should().Be(OrderStatus.Failed);
        _mockKafkaProducer.Verify(p => p.PublishOrderEventAsync(
            It.Is<OrderFailedIntegrationEvent>(e => 
                e.OrderId == order.Id &&
                e.FailureReason.Contains("Insufficient stock"))),
            Times.Once);
    }

    [Fact]
    public async Task SagaFlow_PaymentFailure()
    {
        // Arrange
        var order = CreateTestOrder();
        order.UpdateStatus(OrderStatus.InventoryReserved);
        _mockOrderRepository.Setup(r => r.GetByIdAsync(order.Id))
            .ReturnsAsync(order);

        // Act - Simulate Payment Failure
        await _saga.HandlePaymentFailedEvent(new PaymentFailedEvent(
            order.Id,
            order.CustomerId.ToString(),
            order.TotalAmount,
            "Payment declined",
            "CreditCard"));

        // Assert
        order.Status.Should().Be(OrderStatus.Failed);
        _mockKafkaProducer.Verify(p => p.PublishOrderEventAsync(
            It.Is<ReleaseInventoryEvent>(e => e.OrderId == order.Id)),
            Times.Once);
        _mockKafkaProducer.Verify(p => p.PublishOrderEventAsync(
            It.Is<OrderFailedIntegrationEvent>(e => 
                e.OrderId == order.Id &&
                e.FailureReason.Contains("Payment declined"))),
            Times.Once);
    }

    private Order CreateTestOrder()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 2, 10.00m);
        return order;
    }
} 