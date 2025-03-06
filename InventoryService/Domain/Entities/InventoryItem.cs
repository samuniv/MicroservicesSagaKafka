using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.Domain.Entities
{
    public class InventoryItem
    {
        public Guid Id { get; private set; }
        
        public Guid ProductId { get; private set; }
        public Product Product { get; private set; }
        
        [Range(0, int.MaxValue)]
        public int Quantity { get; private set; }
        
        [Range(0, int.MaxValue)]
        public int Reserved { get; private set; }
        
        public DateTime LastUpdated { get; private set; }

        // For EF Core
        protected InventoryItem() { }

        public InventoryItem(Guid productId, int initialQuantity)
        {
            if (initialQuantity < 0)
                throw new ArgumentException("Initial quantity cannot be negative", nameof(initialQuantity));

            Id = Guid.NewGuid();
            ProductId = productId;
            Quantity = initialQuantity;
            Reserved = 0;
            LastUpdated = DateTime.UtcNow;
        }

        public bool CanReserve(int amount)
        {
            return amount > 0 && (Quantity - Reserved) >= amount;
        }

        public void Reserve(int amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));

            if (!CanReserve(amount))
                throw new InvalidOperationException($"Cannot reserve {amount} items. Available: {Quantity - Reserved}");

            Reserved += amount;
            LastUpdated = DateTime.UtcNow;
        }

        public void Release(int amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));

            if (amount > Reserved)
                throw new InvalidOperationException($"Cannot release {amount} items. Only {Reserved} are reserved");

            Reserved -= amount;
            LastUpdated = DateTime.UtcNow;
        }

        public void Restock(int amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));

            Quantity += amount;
            LastUpdated = DateTime.UtcNow;
        }

        public void RemoveStock(int amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));

            if (amount > Reserved)
                throw new InvalidOperationException($"Cannot remove {amount} items. Only {Reserved} are reserved");

            Quantity -= amount;
            Reserved -= amount;
            LastUpdated = DateTime.UtcNow;
        }

        public int GetAvailableQuantity()
        {
            return Quantity - Reserved;
        }
    }
} 