using OrderService.Domain.Entities;

namespace OrderService.Events.IntegrationEvents;

public class OrderCancelledIntegrationEvent : OrderIntegrationEvent
{
    public string Reason { get; }

    public OrderCancelledIntegrationEvent(Order order, string reason) 
        : base(order.Id, order.CustomerId, order.TotalAmount)
    {
        Reason = reason;
    }
} 