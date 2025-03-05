using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderService.Domain.Entities;

public class Order
{
    private readonly List<OrderItem> _items = new();
    private decimal _totalAmount;

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount 
    { 
        get => _totalAmount;
        private set => _totalAmount = value;
    }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Order() { } // For EF Core

    public Order(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("CustomerId cannot be empty", nameof(customerId));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Created;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        _totalAmount = 0;
    }

    public void AddItem(OrderItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("Cannot add items to an order that is not in Created status");

        _items.Add(item);
        UpdateTotalAmount();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveItem(Guid productId)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("Cannot remove items from an order that is not in Created status");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            _items.Remove(item);
            UpdateTotalAmount();
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void UpdateStatus(OrderStatus newStatus)
    {
        if (!IsValidStatusTransition(Status, newStatus))
            throw new InvalidOperationException($"Cannot transition from {Status} to {newStatus}");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    private void UpdateTotalAmount()
    {
        _totalAmount = _items.Sum(item => item.Subtotal);
    }

    private bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        return (currentStatus, newStatus) switch
        {
            (OrderStatus.Created, OrderStatus.InventoryReserved) => true,
            (OrderStatus.Created, OrderStatus.Cancelled) => true,
            (OrderStatus.InventoryReserved, OrderStatus.PaymentProcessing) => true,
            (OrderStatus.InventoryReserved, OrderStatus.Failed) => true,
            (OrderStatus.PaymentProcessing, OrderStatus.Completed) => true,
            (OrderStatus.PaymentProcessing, OrderStatus.Failed) => true,
            _ => false
        };
    }
} 