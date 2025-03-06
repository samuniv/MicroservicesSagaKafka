namespace InventoryService.Infrastructure.Exceptions;

public class InventoryNotFoundException : Exception
{
    public InventoryNotFoundException(string message) : base(message) { }
    public InventoryNotFoundException(Guid id) : base($"Inventory item with ID {id} was not found.") { }
    public InventoryNotFoundException(string productId, string message) : base($"Inventory item for product {productId} was not found. {message}") { }
}

public class InsufficientStockException : Exception
{
    public string ProductId { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public InsufficientStockException(string productId, int requested, int available)
        : base($"Insufficient stock for product {productId}. Requested: {requested}, Available: {available}")
    {
        ProductId = productId;
        RequestedQuantity = requested;
        AvailableQuantity = available;
    }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public class StockOperationException : Exception
{
    public string ProductId { get; }
    public string Operation { get; }

    public StockOperationException(string productId, string operation, string message)
        : base($"Failed to perform {operation} operation on product {productId}: {message}")
    {
        ProductId = productId;
        Operation = operation;
    }
} 