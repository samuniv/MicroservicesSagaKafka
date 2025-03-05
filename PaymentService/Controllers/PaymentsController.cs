using Microsoft.AspNetCore.Mvc;
using PaymentService.Domain.Models;
using PaymentService.Domain.Repositories;
using PaymentService.Events;
using PaymentService.Infrastructure.MessageBus;
using PaymentService.Models.Requests;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentRepository _repository;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentRepository repository,
        KafkaProducerService kafkaProducer,
        ILogger<PaymentsController> logger)
    {
        _repository = repository;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Payment>> InitiatePayment([FromBody] InitiatePaymentRequest request)
    {
        var payment = new Payment(request.OrderId, request.Amount);
        await _repository.CreateAsync(payment);
        await _repository.SaveChangesAsync();

        var @event = new PaymentInitiatedEvent(request.OrderId, request.Amount);
        await _kafkaProducer.PublishPaymentInitiatedEventAsync(@event);

        _logger.LogInformation("Payment initiated for Order {OrderId} with amount {Amount}", 
            request.OrderId, request.Amount);

        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Payment>> GetPayment(Guid id)
    {
        var payment = await _repository.GetByIdAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        return payment;
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<Payment>> GetPaymentByOrderId(Guid orderId)
    {
        var payment = await _repository.GetByOrderIdAsync(orderId);
        if (payment == null)
        {
            return NotFound();
        }

        return payment;
    }

    [HttpPost("{id}/process")]
    public async Task<IActionResult> ProcessPayment(Guid id, [FromBody] ProcessPaymentRequest request)
    {
        var payment = await _repository.GetByIdAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        try
        {
            payment.Process(request.TransactionId);
            await _repository.UpdateAsync(payment);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Payment {Id} processed with transaction {TransactionId}", 
                id, request.TransactionId);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to process payment {Id}", id);
            
            var failedEvent = new PaymentFailedEvent(
                payment.OrderId,
                request.TransactionId,
                payment.Amount,
                ex.Message);
            
            await _kafkaProducer.PublishPaymentFailedEventAsync(failedEvent);
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompletePayment(Guid id)
    {
        var payment = await _repository.GetByIdAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        try
        {
            payment.Complete();
            await _repository.UpdateAsync(payment);
            await _repository.SaveChangesAsync();

            var @event = new PaymentCompletedEvent(payment.OrderId, payment.TransactionId!, payment.Amount);
            await _kafkaProducer.PublishPaymentCompletedEventAsync(@event);

            _logger.LogInformation("Payment {Id} completed", id);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to complete payment {Id}", id);
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/fail")]
    public async Task<IActionResult> FailPayment(Guid id, [FromBody] string failureReason)
    {
        var payment = await _repository.GetByIdAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        try
        {
            payment.Fail();
            await _repository.UpdateAsync(payment);
            await _repository.SaveChangesAsync();

            var failedEvent = new PaymentFailedEvent(
                payment.OrderId,
                payment.TransactionId,
                payment.Amount,
                failureReason);
            
            await _kafkaProducer.PublishPaymentFailedEventAsync(failedEvent);
            _logger.LogInformation("Payment {Id} failed: {Reason}", id, failureReason);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to mark payment {Id} as failed", id);
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundPaymentRequest request)
    {
        var payment = await _repository.GetByIdAsync(id);
        if (payment == null)
        {
            return NotFound();
        }

        try
        {
            payment.Refund();
            await _repository.UpdateAsync(payment);
            await _repository.SaveChangesAsync();

            var refundEvent = new RefundInitiatedEvent(
                payment.OrderId,
                payment.TransactionId!,
                payment.Amount,
                request.Reason);
            
            await _kafkaProducer.PublishRefundInitiatedEventAsync(refundEvent);
            _logger.LogInformation("Payment {Id} refund initiated: {Reason}", id, request.Reason);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to initiate refund for payment {Id}", id);
            return BadRequest(ex.Message);
        }
    }
}

public record InitiatePaymentRequest(Guid OrderId, decimal Amount);
public record ProcessPaymentRequest(string TransactionId); 