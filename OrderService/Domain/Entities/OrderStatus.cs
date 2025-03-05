namespace OrderService.Domain.Entities;

public enum OrderStatus
{
    Created,
    InventoryReserved,
    PaymentProcessing,
    Completed,
    Failed,
    Cancelled
} 