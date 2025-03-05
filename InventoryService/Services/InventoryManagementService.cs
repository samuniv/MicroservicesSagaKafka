using InventoryService.Domain.Models;
using InventoryService.Events.IntegrationEvents;
using InventoryService.Infrastructure.MessageBus;
using InventoryService.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace InventoryService.Services;

public class InventoryManagementService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly ILogger<InventoryManagementService> _logger;

    public InventoryManagementService(
        IInventoryRepository repository,
        KafkaProducerService kafkaProducer,
        ILogger<InventoryManagementService> logger)
    {
        _repository = repository;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<InventoryItem?> GetInventoryItemAsync(Guid id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<InventoryItem?> GetInventoryItemByProductIdAsync(string productId)
    {
        return await _repository.GetByProductIdAsync(productId);
    }

    public async Task<IEnumerable<InventoryItem>> GetAllInventoryItemsAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(int threshold)
    {
        return await _repository.GetLowStockItemsAsync(threshold);
    }

    public async Task<InventoryItem> CreateInventoryItemAsync(string productId, string name, int quantity, decimal unitPrice, string sku)
    {
        if (await _repository.ExistsByProductIdAsync(productId))
        {
            throw new InvalidOperationException($"Inventory item with ProductId {productId} already exists");
        }

        var item = new InventoryItem(productId, name, quantity, unitPrice, sku);
        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Created new inventory item: {ProductId}, {Name}, Quantity: {Quantity}", 
            productId, name, quantity);

        return item;
    }

    public async Task<bool> ReserveStockAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId) 
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        var success = item.TryReserveStock(quantity);
        if (success)
        {
            await _repository.UpdateAsync(item);
            await _repository.SaveChangesAsync();
            
            // Publish the stock reserved event
            var @event = new StockReservedIntegrationEvent(
                Guid.NewGuid(), // This should be the actual OrderId in a real scenario
                productId,
                quantity,
                item.UnitPrice);
            
            await _kafkaProducer.PublishStockReservedEventAsync(@event);
            
            _logger.LogInformation("Reserved {Quantity} units for product {ProductId}", quantity, productId);
        }
        else
        {
            _logger.LogWarning("Failed to reserve {Quantity} units for product {ProductId}. Available: {Available}", 
                quantity, productId, item.QuantityAvailable);
        }

        return success;
    }

    public async Task ConfirmReservationAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.ConfirmReservation(quantity);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Confirmed reservation of {Quantity} units for product {ProductId}", 
            quantity, productId);
    }

    public async Task CancelReservationAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.CancelReservation(quantity);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Cancelled reservation of {Quantity} units for product {ProductId}", 
            quantity, productId);
    }

    public async Task AddStockAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.AddStock(quantity);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Added {Quantity} units to product {ProductId}", quantity, productId);
    }

    public async Task RemoveStockAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.RemoveStock(quantity);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Removed {Quantity} units from product {ProductId}", quantity, productId);
    }

    public async Task UpdatePriceAsync(string productId, decimal newPrice)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.UpdateUnitPrice(newPrice);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Updated price for product {ProductId} to {NewPrice}", productId, newPrice);
    }

    public async Task DeleteInventoryItemAsync(Guid id)
    {
        if (!await _repository.ExistsAsync(id))
        {
            throw new KeyNotFoundException($"Inventory item with Id {id} not found");
        }

        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Deleted inventory item {Id}", id);
    }
} 