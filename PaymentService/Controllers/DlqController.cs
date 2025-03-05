using Microsoft.AspNetCore.Mvc;
using PaymentService.Infrastructure.MessageBus;
using PaymentService.Models;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DlqController : ControllerBase
{
    private readonly DlqMonitoringService _dlqMonitoringService;
    private readonly ILogger<DlqController> _logger;

    public DlqController(
        DlqMonitoringService dlqMonitoringService,
        ILogger<DlqController> logger)
    {
        _dlqMonitoringService = dlqMonitoringService;
        _logger = logger;
    }

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(DlqStatistics), StatusCodes.Status200OK)]
    public ActionResult<DlqStatistics> GetStatistics()
    {
        return Ok(_dlqMonitoringService.GetStatistics());
    }

    [HttpPost("retry/{messageKey}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryMessage(string messageKey)
    {
        var result = await _dlqMonitoringService.RetryMessageAsync(messageKey);
        if (!result)
        {
            return NotFound($"Message with key {messageKey} not found in DLQ");
        }

        _logger.LogInformation("Successfully retried message {MessageKey}", messageKey);
        return Ok($"Message {messageKey} has been retried");
    }

    [HttpPost("retry-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryAllMessages()
    {
        var result = await _dlqMonitoringService.RetryAllMessagesAsync();
        if (!result)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, "Some messages failed to retry");
        }

        _logger.LogInformation("Successfully retried all DLQ messages");
        return Ok("All messages have been retried");
    }
} 