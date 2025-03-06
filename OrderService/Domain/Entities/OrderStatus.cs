namespace OrderService.Domain.Entities;

public enum OrderStatus
{
    Created = 0,
    InventoryReserved = 1,
    PaymentProcessing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
} 