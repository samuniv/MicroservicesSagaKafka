using OrderService.Domain.Entities;

namespace OrderService.Events.IntegrationEvents;

public class OrderCreatedIntegrationEvent : OrderIntegrationEvent
{
    public IReadOnlyList<OrderItemEvent> Items { get; }

    public OrderCreatedIntegrationEvent(Order order) 
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

public class OrderItemEvent
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal Subtotal { get; init; }
} 