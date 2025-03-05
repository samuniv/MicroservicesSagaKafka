using OrderService.Domain.Entities;

namespace OrderService.Events.IntegrationEvents;

public class OrderStatusChangedIntegrationEvent : OrderIntegrationEvent
{
    public string OldStatus { get; }
    public string NewStatus { get; }

    public OrderStatusChangedIntegrationEvent(Order order, string oldStatus, string newStatus) 
        : base(order.Id, order.CustomerId, order.TotalAmount)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
} 