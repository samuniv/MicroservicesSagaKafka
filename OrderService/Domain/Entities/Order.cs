using System;
using System.Collections.Generic;
using System.Linq;
using OrderService.Domain.ValueObjects;
using OrderService.Domain.Exceptions;

namespace OrderService.Domain.Entities;

public class Order
{
    private readonly List<OrderItem> _items;

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // For EF Core
    protected Order()
    {
        _items = new List<OrderItem>();
    }

    public Order(Guid customerId)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId cannot be empty", nameof(customerId));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Created;
        _items = new List<OrderItem>();
        CreatedAt = DateTime.UtcNow;
        TotalAmount = 0;
    }

    public void AddItem(Guid productId, int quantity, decimal price)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOrderStateException("Cannot modify items after order is processed");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            var newQuantity = existingItem.Quantity + quantity;
            _items.Remove(existingItem);
            _items.Add(existingItem.UpdateQuantity(newQuantity));
        }
        else
        {
            _items.Add(new OrderItem(productId, quantity, price));
        }

        UpdateTotalAmount();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveItem(Guid productId)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOrderStateException("Cannot modify items after order is processed");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            throw new ArgumentException($"Item with ProductId {productId} not found in order");

        _items.Remove(item);
        UpdateTotalAmount();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(OrderStatus newStatus)
    {
        if (!IsValidStatusTransition(newStatus))
            throw new InvalidOrderStateException($"Invalid status transition from {Status} to {newStatus}");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    private bool IsValidStatusTransition(OrderStatus newStatus)
    {
        return (Status, newStatus) switch
        {
            (OrderStatus.Created, OrderStatus.InventoryReserved) => true,
            (OrderStatus.Created, OrderStatus.Cancelled) => true,
            (OrderStatus.InventoryReserved, OrderStatus.PaymentProcessing) => true,
            (OrderStatus.InventoryReserved, OrderStatus.Cancelled) => true,
            (OrderStatus.PaymentProcessing, OrderStatus.Completed) => true,
            (OrderStatus.PaymentProcessing, OrderStatus.Failed) => true,
            _ => false
        };
    }

    private void UpdateTotalAmount()
    {
        TotalAmount = _items.Sum(item => item.Subtotal);
    }

    public void ValidateState()
    {
        if (!_items.Any())
            throw new InvalidOrderStateException("Order must have at least one item");

        if (TotalAmount != _items.Sum(item => item.Subtotal))
            throw new InvalidOrderStateException("Order total amount does not match items subtotal");
    }
} 