using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PaymentService.Infrastructure.MessageBus;
using PaymentService.Infrastructure.Auth;
using PaymentService.Infrastructure.RateLimiting;
using PaymentService.Models;

namespace PaymentService.Controllers;

/// <summary>
/// Controller for managing Dead Letter Queue (DLQ) operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DlqController : ControllerBase
{
    private readonly DlqMonitoringService _dlqMonitoringService;
    private readonly ILogger<DlqController> _logger;

    /// <summary>
    /// Initializes a new instance of the DlqController
    /// </summary>
    /// <param name="dlqMonitoringService">Service for monitoring DLQ messages</param>
    /// <param name="logger">Logger instance</param>
    public DlqController(
        DlqMonitoringService dlqMonitoringService,
        ILogger<DlqController> logger)
    {
        _dlqMonitoringService = dlqMonitoringService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves statistics about the Dead Letter Queue
    /// </summary>
    /// <remarks>
    /// This endpoint provides information about:
    /// - Total number of messages in DLQ
    /// - Messages received in the last hour
    /// - Messages grouped by topic and error type
    /// - Recent message details
    /// 
    /// Required Role: Admin or Support
    /// Required Permission: dlq_permissions:view
    /// </remarks>
    /// <response code="200">Returns the DLQ statistics</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User lacks required permissions</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpGet("statistics")]
    [Authorize(Policy = Policies.ViewDlqStatistics)]
    [RateLimiting("Statistics")]
    [ProducesResponseType(typeof(DlqStatistics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<DlqStatistics> GetStatistics()
    {
        _logger.LogInformation(
            "User {User} requesting DLQ statistics",
            User.Identity?.Name);

        return Ok(_dlqMonitoringService.GetStatistics());
    }

    /// <summary>
    /// Retries processing of a specific message from the DLQ
    /// </summary>
    /// <remarks>
    /// This endpoint attempts to:
    /// - Retrieve the message from DLQ
    /// - Republish it to its original topic
    /// - Remove it from the DLQ if successful
    /// 
    /// Required Role: Admin
    /// Required Permission: dlq_permissions:retry
    /// </remarks>
    /// <param name="messageKey">The unique identifier of the message to retry</param>
    /// <response code="200">Message successfully retried</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User lacks required permissions</response>
    /// <response code="404">Message not found in DLQ</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpPost("retry/{messageKey}")]
    [Authorize(Policy = Policies.RetryDlqMessages)]
    [RateLimiting("RetryMessage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RetryMessage(string messageKey)
    {
        _logger.LogInformation(
            "User {User} attempting to retry message {MessageKey}",
            User.Identity?.Name,
            messageKey);

        if (!User.HasClaim("dlq_permissions", "retry"))
        {
            _logger.LogWarning(
                "User {User} denied retry permission for message {MessageKey}",
                User.Identity?.Name,
                messageKey);
            return Forbid();
        }

        var result = await _dlqMonitoringService.RetryMessageAsync(messageKey);
        if (!result)
        {
            return NotFound($"Message with key {messageKey} not found in DLQ");
        }

        _logger.LogInformation(
            "User {User} successfully retried message {MessageKey}",
            User.Identity?.Name,
            messageKey);

        return Ok($"Message {messageKey} has been retried");
    }

    /// <summary>
    /// Retries processing of all messages in the DLQ
    /// </summary>
    /// <remarks>
    /// This endpoint attempts to:
    /// - Retrieve all messages from DLQ
    /// - Republish them to their original topics
    /// - Remove successfully processed messages from the DLQ
    /// 
    /// Required Role: Admin
    /// Required Permission: dlq_permissions:retry_all
    /// 
    /// Warning: This operation can be resource-intensive for large DLQs
    /// </remarks>
    /// <response code="200">All messages successfully retried</response>
    /// <response code="207">Some messages failed to retry</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User lacks required permissions</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpPost("retry-all")]
    [Authorize(Policy = Policies.RetryAllDlqMessages)]
    [RateLimiting("RetryAll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RetryAllMessages()
    {
        _logger.LogInformation(
            "User {User} attempting to retry all DLQ messages",
            User.Identity?.Name);

        if (!User.HasClaim("dlq_permissions", "retry_all"))
        {
            _logger.LogWarning(
                "User {User} denied retry-all permission",
                User.Identity?.Name);
            return Forbid();
        }

        var result = await _dlqMonitoringService.RetryAllMessagesAsync();
        if (!result)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, "Some messages failed to retry");
        }

        _logger.LogInformation(
            "User {User} successfully retried all DLQ messages",
            User.Identity?.Name);

        return Ok("All messages have been retried");
    }

    /// <summary>
    /// Retrieves the current DLQ settings
    /// </summary>
    /// <remarks>
    /// This endpoint provides information about:
    /// - Message retention period
    /// - Cleanup interval
    /// - Maximum retry attempts
    /// - DLQ status
    /// 
    /// Required Role: Admin
    /// Required Permission: dlq_permissions:manage
    /// </remarks>
    /// <response code="200">Returns the current DLQ settings</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User lacks required permissions</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpGet("settings")]
    [Authorize(Policy = Policies.ManageDlqSettings)]
    [RateLimiting("Statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            RetentionPeriod = TimeSpan.FromDays(7),
            CleanupInterval = TimeSpan.FromHours(1),
            MaxRetries = 3,
            EnableDlq = true
        });
    }
} 