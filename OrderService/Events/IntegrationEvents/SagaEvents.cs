using OrderService.Domain.Entities;

namespace OrderService.Events.IntegrationEvents;

public class RequestInventoryReservationEvent : OrderIntegrationEvent
{
    public IReadOnlyList<OrderItemEvent> Items { get; }

    public RequestInventoryReservationEvent(Order order) 
        : base(order.Id, order.CustomerId, order.TotalAmount)
    {
        Items = order.Items.Select(item => new OrderItemEvent
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            Price = item.Price,
            Subtotal = item.Subtotal
        }).ToList();
    }
}

public class InventoryReservedEvent : OrderIntegrationEvent
{
    public IReadOnlyList<OrderItemEvent> ReservedItems { get; }

    public InventoryReservedEvent(Guid orderId, string customerId, decimal totalAmount, IReadOnlyList<OrderItemEvent> reservedItems) 
        : base(orderId, customerId, totalAmount)
    {
        ReservedItems = reservedItems;
    }
}

public class InventoryReservationFailedEvent : OrderIntegrationEvent
{
    public string Reason { get; }

    public InventoryReservationFailedEvent(Guid orderId, string customerId, decimal totalAmount, string reason) 
        : base(orderId, customerId, totalAmount)
    {
        Reason = reason;
    }
}

public class RequestPaymentProcessingEvent : OrderIntegrationEvent
{
    public string PaymentMethod { get; }
    public string Currency { get; }

    public RequestPaymentProcessingEvent(Order order) 
        : base(order.Id, order.CustomerId, order.TotalAmount)
    {
        PaymentMethod = "Default"; // This could come from order details
        Currency = "USD"; // This could be configurable
    }
}

public class PaymentCompletedEvent : OrderIntegrationEvent
{
    public string TransactionId { get; }
    public DateTime ProcessedAt { get; }

    public PaymentCompletedEvent(Guid orderId, string customerId, decimal totalAmount, string transactionId) 
        : base(orderId, customerId, totalAmount)
    {
        TransactionId = transactionId;
        ProcessedAt = DateTime.UtcNow;
    }
}

public class PaymentFailedEvent : OrderIntegrationEvent
{
    public string FailureReason { get; }
    public string PaymentMethod { get; }

    public PaymentFailedEvent(Guid orderId, string customerId, decimal totalAmount, string failureReason, string paymentMethod) 
        : base(orderId, customerId, totalAmount)
    {
        FailureReason = failureReason;
        PaymentMethod = paymentMethod;
    }
}

public class ReleaseInventoryEvent : OrderIntegrationEvent
{
    public IReadOnlyList<OrderItemEvent> Items { get; }

    public ReleaseInventoryEvent(Order order) 
        : base(order.Id, order.CustomerId, order.TotalAmount)
    {
        Items = order.Items.Select(item => new OrderItemEvent
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            Price = item.Price,
            Subtotal = item.Subtotal
        }).ToList();
    }
}

public class OrderFailedIntegrationEvent : OrderIntegrationEvent
{
    public string FailureReason { get; }
    public OrderStatus PreviousStatus { get; }

    public OrderFailedIntegrationEvent(Order order, string failureReason) 
        : base(order.Id, order.CustomerId, order.TotalAmount)
    {
        FailureReason = failureReason;
        PreviousStatus = order.Status;
    }
} 