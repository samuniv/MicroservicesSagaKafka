using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.MessageBus;
using PaymentService.Infrastructure.Repositories;
using PaymentService.Events.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace PaymentService.Tests.Integration;

public class PaymentServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _dbContext;
    private readonly PaymentRepository _repository;
    private readonly Mock<KafkaProducerService> _mockKafkaProducer;
    private readonly Mock<ILogger<Services.PaymentService>> _mockLogger;
    private readonly Services.PaymentService _service;

    public PaymentServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PaymentDbContext(options);
        _repository = new PaymentRepository(_dbContext);
        _mockKafkaProducer = new Mock<KafkaProducerService>(Mock.Of<ILogger<KafkaProducerService>>());
        _mockLogger = new Mock<ILogger<Services.PaymentService>>();
        _service = new Services.PaymentService(_repository, _mockKafkaProducer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessPayment_WithValidData_ShouldSucceed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 99.99m;

        // Act
        var payment = await _service.ProcessPaymentAsync(orderId, amount);

        // Assert
        payment.Should().NotBeNull();
        payment.Status.Should().Be(PaymentStatus.Processing);
        payment.TransactionId.Should().NotBeNullOrEmpty();

        _mockKafkaProducer.Verify(x => x.PublishPaymentEventAsync(
            It.Is<PaymentProcessingEvent>(e => 
                e.OrderId == orderId && 
                e.Amount == amount)),
            Times.Once);
    }

    [Fact]
    public async Task CompletePayment_ShouldUpdateStatusAndPublishEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var payment = await _service.ProcessPaymentAsync(orderId, 99.99m);

        // Act
        await _service.CompletePaymentAsync(payment.Id);

        // Assert
        var completedPayment = await _repository.GetByIdAsync(payment.Id);
        completedPayment.Should().NotBeNull();
        completedPayment!.Status.Should().Be(PaymentStatus.Completed);

        _mockKafkaProducer.Verify(x => x.PublishPaymentEventAsync(
            It.Is<PaymentCompletedEvent>(e => 
                e.OrderId == orderId && 
                e.TransactionId == payment.TransactionId)),
            Times.Once);
    }

    [Fact]
    public async Task FailPayment_ShouldUpdateStatusAndPublishEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var payment = await _service.ProcessPaymentAsync(orderId, 99.99m);
        var failureReason = "Insufficient funds";

        // Act
        await _service.FailPaymentAsync(payment.Id, failureReason);

        // Assert
        var failedPayment = await _repository.GetByIdAsync(payment.Id);
        failedPayment.Should().NotBeNull();
        failedPayment!.Status.Should().Be(PaymentStatus.Failed);
        failedPayment.FailureReason.Should().Be(failureReason);

        _mockKafkaProducer.Verify(x => x.PublishPaymentEventAsync(
            It.Is<PaymentFailedEvent>(e => 
                e.OrderId == orderId && 
                e.FailureReason == failureReason)),
            Times.Once);
    }

    [Fact]
    public async Task RefundPayment_ShouldUpdateStatusAndPublishEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var payment = await _service.ProcessPaymentAsync(orderId, 99.99m);
        await _service.CompletePaymentAsync(payment.Id);
        var refundReason = "Customer request";

        // Act
        await _service.RefundPaymentAsync(payment.Id, refundReason);

        // Assert
        var refundedPayment = await _repository.GetByIdAsync(payment.Id);
        refundedPayment.Should().NotBeNull();
        refundedPayment!.Status.Should().Be(PaymentStatus.Refunded);
        refundedPayment.RefundReason.Should().Be(refundReason);

        _mockKafkaProducer.Verify(x => x.PublishPaymentEventAsync(
            It.Is<PaymentRefundedEvent>(e => 
                e.OrderId == orderId && 
                e.RefundReason == refundReason)),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentByOrderId_ShouldReturnCorrectPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var payment = await _service.ProcessPaymentAsync(orderId, 99.99m);

        // Act
        var retrievedPayment = await _service.GetPaymentByOrderIdAsync(orderId);

        // Assert
        retrievedPayment.Should().NotBeNull();
        retrievedPayment!.Id.Should().Be(payment.Id);
        retrievedPayment.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task GetPaymentHistory_ShouldReturnAllPaymentsForOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await _service.ProcessPaymentAsync(orderId, 99.99m); // First attempt
        await _service.ProcessPaymentAsync(orderId, 99.99m); // Second attempt

        // Act
        var paymentHistory = await _service.GetPaymentHistoryAsync(orderId);

        // Assert
        paymentHistory.Should().HaveCount(2);
        paymentHistory.Should().AllSatisfy(p => p.OrderId.Should().Be(orderId));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
} 