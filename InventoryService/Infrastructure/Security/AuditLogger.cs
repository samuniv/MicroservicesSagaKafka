using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

namespace InventoryService.Infrastructure.Security
{
    public class AuditLogger
    {
        private readonly ILogger<AuditLogger> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogger(
            ILogger<AuditLogger> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogSecurityEventAsync(
            string eventType,
            string resourceType,
            string resourceId,
            string action,
            bool success,
            string? details = null)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var userRoles = user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();
            var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var auditEvent = new AuditEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Action = action,
                UserId = userId,
                UserRoles = userRoles,
                IpAddress = ipAddress,
                Success = success,
                Details = details,
                CorrelationId = _httpContextAccessor.HttpContext?.TraceIdentifier
            };

            // Log structured audit event
            _logger.LogInformation(
                "Security Event: {EventType} | Resource: {ResourceType}/{ResourceId} | Action: {Action} | User: {UserId} | Success: {Success} | IP: {IpAddress} | CorrelationId: {CorrelationId}",
                auditEvent.EventType,
                auditEvent.ResourceType,
                auditEvent.ResourceId,
                auditEvent.Action,
                auditEvent.UserId,
                auditEvent.Success,
                auditEvent.IpAddress,
                auditEvent.CorrelationId);

            // Store full audit event details
            await StoreAuditEventAsync(auditEvent);
        }

        private Task StoreAuditEventAsync(AuditEvent auditEvent)
        {
            // In a production environment, you would:
            // 1. Store in a separate audit database
            // 2. Use a secure storage mechanism
            // 3. Implement retention policies
            // 4. Ensure immutability of audit records
            
            // For now, we'll just serialize and log the full event
            var eventJson = JsonSerializer.Serialize(auditEvent, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            _logger.LogInformation("Full Audit Event: {AuditEvent}", eventJson);
            
            return Task.CompletedTask;
        }
    }

    public class AuditEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public List<string> UserRoles { get; set; } = new();
        public string IpAddress { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Details { get; set; }
        public string? CorrelationId { get; set; }
    }
} 