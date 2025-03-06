using System;

namespace InventoryService.Events.IntegrationEvents
{
    public abstract class BaseIntegrationEvent
    {
        public Guid Id { get; }
        public DateTime CreationDate { get; }
        public string EventType { get; }
        public string CorrelationId { get; }

        protected BaseIntegrationEvent(string eventType, string correlationId = null)
        {
            Id = Guid.NewGuid();
            CreationDate = DateTime.UtcNow;
            EventType = eventType;
            CorrelationId = correlationId ?? Id.ToString();
        }
    }
} 