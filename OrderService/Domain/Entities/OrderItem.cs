using System;

namespace OrderService.Domain.Entities;

public class OrderItem
{
    private decimal _subtotal;

    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }
    public decimal Subtotal 
    { 
        get => _subtotal;
        private set => _subtotal = value;
    }

    private OrderItem() { } // For EF Core

    public OrderItem(Guid productId, int quantity, decimal price)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty", nameof(productId));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (price < 0)
            throw new ArgumentException("Price cannot be negative", nameof(price));

        ProductId = productId;
        Quantity = quantity;
        Price = price;
        UpdateSubtotal();
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(newQuantity));

        Quantity = newQuantity;
        UpdateSubtotal();
    }

    private void UpdateSubtotal()
    {
        _subtotal = Quantity * Price;
    }
} 