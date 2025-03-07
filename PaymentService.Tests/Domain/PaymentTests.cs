using PaymentService.Domain.Entities;
using PaymentService.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace PaymentService.Tests.Domain;

public class PaymentTests
{
    [Fact]
    public void CreatePayment_WithValidData_ShouldSucceed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 99.99m;

        // Act
        var payment = new Payment(orderId, amount);

        // Assert
        payment.Should().NotBeNull();
        payment.Id.Should().NotBe(Guid.Empty);
        payment.OrderId.Should().Be(orderId);
        payment.Amount.Should().Be(amount);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        payment.TransactionId.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreatePayment_WithInvalidAmount_ShouldThrowException(decimal invalidAmount)
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act & Assert
        var act = () => new Payment(orderId, invalidAmount);
        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("Payment amount must be greater than zero");
    }

    [Fact]
    public void ProcessPayment_FromPendingState_ShouldSucceed()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);

        // Act
        payment.Process("TX123");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Processing);
        payment.TransactionId.Should().Be("TX123");
        payment.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ProcessPayment_FromNonPendingState_ShouldThrowException()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);
        payment.Process("TX123");
        payment.Complete();

        // Act & Assert
        var act = () => payment.Process("TX456");
        act.Should().Throw<InvalidPaymentStateException>()
            .WithMessage("Cannot process payment that is not in Pending state");
    }

    [Fact]
    public void CompletePayment_FromProcessingState_ShouldSucceed()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);
        payment.Process("TX123");

        // Act
        payment.Complete();

        // Assert
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CompletePayment_FromNonProcessingState_ShouldThrowException()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);

        // Act & Assert
        var act = () => payment.Complete();
        act.Should().Throw<InvalidPaymentStateException>()
            .WithMessage("Cannot complete payment that is not in Processing state");
    }

    [Fact]
    public void FailPayment_FromProcessingState_ShouldSucceed()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);
        payment.Process("TX123");
        var failureReason = "Insufficient funds";

        // Act
        payment.Fail(failureReason);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be(failureReason);
        payment.FailedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RefundPayment_FromCompletedState_ShouldSucceed()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);
        payment.Process("TX123");
        payment.Complete();

        // Act
        payment.Refund("Customer request");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundReason.Should().Be("Customer request");
        payment.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RefundPayment_FromNonCompletedState_ShouldThrowException()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);

        // Act & Assert
        var act = () => payment.Refund("Customer request");
        act.Should().Throw<InvalidPaymentStateException>()
            .WithMessage("Cannot refund payment that is not in Completed state");
    }

    [Fact]
    public void ValidateState_WithValidPayment_ShouldSucceed()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);

        // Act & Assert
        var act = () => payment.ValidateState();
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateState_WithInvalidTransactionId_ShouldThrowException()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), 99.99m);
        payment.Process(string.Empty);

        // Act & Assert
        var act = () => payment.ValidateState();
        act.Should().Throw<InvalidPaymentException>()
            .WithMessage("Transaction ID cannot be empty when payment is being processed");
    }
} 