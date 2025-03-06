using System;

namespace OrderService.Domain.ValueObjects
{
    public class OrderItem
    {
        public Guid ProductId { get; }
        public int Quantity { get; }
        public decimal Price { get; }
        public decimal Subtotal { get; }

        private OrderItem() { } // For EF Core

        public OrderItem(Guid productId, int quantity, decimal price)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException("ProductId cannot be empty", nameof(productId));

            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

            if (price <= 0)
                throw new ArgumentException("Price must be greater than zero", nameof(price));

            ProductId = productId;
            Quantity = quantity;
            Price = price;
            Subtotal = CalculateSubtotal();
        }

        private decimal CalculateSubtotal()
        {
            return Price * Quantity;
        }

        public OrderItem UpdateQuantity(int newQuantity)
        {
            return new OrderItem(ProductId, newQuantity, Price);
        }

        public OrderItem UpdatePrice(decimal newPrice)
        {
            return new OrderItem(ProductId, Quantity, newPrice);
        }

        // Value object equality
        public override bool Equals(object obj)
        {
            if (obj is not OrderItem other)
                return false;

            return ProductId == other.ProductId &&
                   Quantity == other.Quantity &&
                   Price == other.Price;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ProductId, Quantity, Price);
        }

        public static bool operator ==(OrderItem left, OrderItem right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);

            return left.Equals(right);
        }

        public static bool operator !=(OrderItem left, OrderItem right)
        {
            return !(left == right);
        }
    }
} 