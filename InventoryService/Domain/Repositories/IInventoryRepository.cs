using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryService.Domain.Entities;

namespace InventoryService.Domain.Repositories
{
    public interface IInventoryRepository
    {
        Task<InventoryItem> GetByIdAsync(Guid id);
        Task<InventoryItem> GetByProductIdAsync(Guid productId);
        Task<IEnumerable<InventoryItem>> GetAllAsync();
        Task<IEnumerable<InventoryItem>> GetByProductIdsAsync(IEnumerable<Guid> productIds);
        
        Task<bool> UpdateStockAsync(Guid productId, int quantity);
        Task<bool> ReserveStockAsync(Guid productId, int quantity);
        Task<bool> ReleaseStockAsync(Guid productId, int quantity);
        
        Task AddAsync(InventoryItem item);
        Task UpdateAsync(InventoryItem item);
        Task<bool> DeleteAsync(Guid id);
        
        Task<bool> ProductExistsAsync(Guid productId);
        Task<int> SaveChangesAsync();
    }
} 