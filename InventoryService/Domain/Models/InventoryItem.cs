using System;

namespace InventoryService.Domain.Models;

public class InventoryItem
{
    public Guid Id { get; private set; }
    public string ProductId { get; private set; }
    public string Name { get; private set; }
    public int QuantityAvailable { get; private set; }
    public int QuantityReserved { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string SKU { get; private set; }
    public DateTime LastUpdated { get; private set; }

    private InventoryItem() { }

    public InventoryItem(string productId, string name, int initialQuantity, decimal unitPrice, string sku)
    {
        Id = Guid.NewGuid();
        ProductId = productId ?? throw new ArgumentNullException(nameof(productId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        QuantityAvailable = initialQuantity;
        QuantityReserved = 0;
        UnitPrice = unitPrice > 0 ? unitPrice : throw new ArgumentException("Unit price must be greater than zero", nameof(unitPrice));
        SKU = sku ?? throw new ArgumentNullException(nameof(sku));
        LastUpdated = DateTime.UtcNow;
    }

    public bool TryReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (QuantityAvailable >= quantity)
        {
            QuantityAvailable -= quantity;
            QuantityReserved += quantity;
            LastUpdated = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    public void ConfirmReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (QuantityReserved < quantity)
            throw new InvalidOperationException($"Not enough reserved stock. Reserved: {QuantityReserved}, Requested: {quantity}");

        QuantityReserved -= quantity;
        LastUpdated = DateTime.UtcNow;
    }

    public void CancelReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (QuantityReserved < quantity)
            throw new InvalidOperationException($"Not enough reserved stock to cancel. Reserved: {QuantityReserved}, Requested: {quantity}");

        QuantityReserved -= quantity;
        QuantityAvailable += quantity;
        LastUpdated = DateTime.UtcNow;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        QuantityAvailable += quantity;
        LastUpdated = DateTime.UtcNow;
    }

    public void RemoveStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (QuantityAvailable < quantity)
            throw new InvalidOperationException($"Not enough available stock to remove. Available: {QuantityAvailable}, Requested: {quantity}");

        QuantityAvailable -= quantity;
        LastUpdated = DateTime.UtcNow;
    }

    public void UpdateUnitPrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(newPrice));

        UnitPrice = newPrice;
        LastUpdated = DateTime.UtcNow;
    }
} 