using InventoryService.Domain.Models;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<InventoryItem?> GetInventoryItemAsync(Guid id);
    Task<InventoryItem?> GetInventoryItemByProductIdAsync(string productId);
    Task<IEnumerable<InventoryItem>> GetAllInventoryItemsAsync();
    Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(int threshold);
    Task<InventoryItem> CreateInventoryItemAsync(string productId, string name, int quantity, decimal unitPrice, string sku);
    Task<bool> ReserveStockAsync(string productId, int quantity);
    Task ConfirmReservationAsync(string productId, int quantity);
    Task CancelReservationAsync(string productId, int quantity);
    Task AddStockAsync(string productId, int quantity);
    Task RemoveStockAsync(string productId, int quantity);
    Task UpdatePriceAsync(string productId, decimal newPrice);
    Task DeleteInventoryItemAsync(Guid id);
} 