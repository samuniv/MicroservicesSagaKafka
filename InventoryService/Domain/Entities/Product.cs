using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; private set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; private set; }
        
        [Required]
        [StringLength(50)]
        public string SKU { get; private set; }
        
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; private set; }
        
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        // For EF Core
        protected Product() { }

        public Product(string name, string sku, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));
            
            if (string.IsNullOrWhiteSpace(sku))
                throw new ArgumentException("SKU cannot be empty", nameof(sku));
            
            if (price <= 0)
                throw new ArgumentException("Price must be greater than zero", nameof(price));

            Id = Guid.NewGuid();
            Name = name;
            SKU = sku.ToUpperInvariant();
            Price = price;
            CreatedAt = DateTime.UtcNow;
        }

        public void UpdateDetails(string name, string sku, decimal price)
        {
            if (!string.IsNullOrWhiteSpace(name))
                Name = name;
            
            if (!string.IsNullOrWhiteSpace(sku))
                SKU = sku.ToUpperInvariant();
            
            if (price > 0)
                Price = price;

            UpdatedAt = DateTime.UtcNow;
        }
    }
} 