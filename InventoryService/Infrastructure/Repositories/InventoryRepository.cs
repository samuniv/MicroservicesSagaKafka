using InventoryService.Domain.Models;
using InventoryService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryItem?> GetByIdAsync(Guid id)
    {
        return await _context.InventoryItems.FindAsync(id);
    }

    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        return await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.ProductId == productId);
    }

    public async Task<InventoryItem?> GetBySkuAsync(string sku)
    {
        return await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.SKU == sku);
    }

    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        return await _context.InventoryItems.ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(int threshold)
    {
        return await _context.InventoryItems
            .Where(i => i.QuantityAvailable <= threshold)
            .ToListAsync();
    }

    public async Task AddAsync(InventoryItem item)
    {
        await _context.InventoryItems.AddAsync(item);
    }

    public async Task UpdateAsync(InventoryItem item)
    {
        _context.Entry(item).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var item = await GetByIdAsync(id);
        if (item != null)
        {
            _context.InventoryItems.Remove(item);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.InventoryItems.AnyAsync(i => i.Id == id);
    }

    public async Task<bool> ExistsByProductIdAsync(string productId)
    {
        return await _context.InventoryItems.AnyAsync(i => i.ProductId == productId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
} 