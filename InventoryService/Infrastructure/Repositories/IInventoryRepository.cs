using InventoryService.Domain.Models;

namespace InventoryService.Infrastructure.Repositories;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByIdAsync(Guid id);
    Task<InventoryItem?> GetByProductIdAsync(string productId);
    Task<InventoryItem?> GetBySkuAsync(string sku);
    Task<IEnumerable<InventoryItem>> GetAllAsync();
    Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(int threshold);
    Task AddAsync(InventoryItem item);
    Task UpdateAsync(InventoryItem item);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> ExistsByProductIdAsync(string productId);
    Task SaveChangesAsync();
} 