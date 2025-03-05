using System.ComponentModel.DataAnnotations;

namespace PaymentService.Models.Requests;

public record InitiatePaymentRequest(
    [Required]
    Guid OrderId,
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    decimal Amount);

public record ProcessPaymentRequest(
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "TransactionId must be between 1 and 100 characters")]
    string TransactionId);

public record RefundPaymentRequest(
    [Required]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    string Reason = "Customer requested refund"); 