using InventoryService.Domain.Models;
using InventoryService.Events.IntegrationEvents;
using InventoryService.Infrastructure.MessageBus;
using InventoryService.Infrastructure.Repositories;
using InventoryService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace InventoryService.Services;

public class InventoryManagementService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly ILogger<InventoryManagementService> _logger;
    private readonly InventorySettings _settings;

    public InventoryManagementService(
        IInventoryRepository repository,
        KafkaProducerService kafkaProducer,
        IOptions<InventorySettings> settings,
        ILogger<InventoryManagementService> logger)
    {
        _repository = repository;
        _kafkaProducer = kafkaProducer;
        _settings = settings.Value;
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

        await CheckAndPublishStockLevelEvents(item);

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
            
            var @event = new StockReservedIntegrationEvent(
                Guid.NewGuid(), // This should be the actual OrderId in a real scenario
                productId,
                quantity,
                item.UnitPrice);
            
            await _kafkaProducer.PublishStockReservedEventAsync(@event);
            await CheckAndPublishStockLevelEvents(item);

            _logger.LogInformation("Reserved {Quantity} units for product {ProductId}", quantity, productId);
        }
        else
        {
            _logger.LogWarning("Failed to reserve {Quantity} units for product {ProductId}. Available: {Available}", 
                quantity, productId, item.QuantityAvailable);

            var failedItem = new FailedReservationItem(
                item.ProductId,
                item.Name,
                quantity,
                item.QuantityAvailable);

            var failedEvent = new ReservationFailedIntegrationEvent(
                Guid.NewGuid(), // This should be the actual OrderId in a real scenario
                new List<FailedReservationItem> { failedItem },
                "Insufficient stock available");

            await _kafkaProducer.PublishReservationFailedEventAsync(failedEvent);
        }

        return success;
    }

    private async Task CheckAndPublishStockLevelEvents(InventoryItem item)
    {
        // Check for stock depletion
        if (item.QuantityAvailable == 0)
        {
            var depletedEvent = new StockDepletedIntegrationEvent(
                item.ProductId,
                item.Name,
                item.QuantityReserved,
                item.QuantityReserved > 0);

            await _kafkaProducer.PublishStockDepletedEventAsync(depletedEvent);
        }
        // Check for warning level
        else if (item.QuantityAvailable <= _settings.Thresholds.WarningLevel)
        {
            var warningEvent = new StockLevelWarningIntegrationEvent(
                item.ProductId,
                item.Name,
                item.QuantityAvailable,
                _settings.Thresholds.WarningLevel);

            await _kafkaProducer.PublishStockLevelWarningEventAsync(warningEvent);
        }
        // Check if stock returned to normal
        else if (item.QuantityAvailable >= _settings.Thresholds.NormalLevel)
        {
            var normalizedEvent = new StockLevelNormalizedIntegrationEvent(
                item.ProductId,
                item.Name,
                item.QuantityAvailable,
                _settings.Thresholds.NormalLevel);

            await _kafkaProducer.PublishStockLevelNormalizedEventAsync(normalizedEvent);
        }

        // Check for auto-reorder
        if (_settings.Thresholds.EnableAutoReorder && 
            item.QuantityAvailable <= _settings.Thresholds.ReorderPoint)
        {
            _logger.LogInformation(
                "Auto-reorder triggered for product {ProductId}. Current quantity: {CurrentQuantity}, Reorder quantity: {ReorderQuantity}",
                item.ProductId,
                item.QuantityAvailable,
                _settings.Thresholds.ReorderQuantity);
            
            // Here you could publish an event or call a service to handle the reorder
        }
    }

    public async Task ConfirmReservationAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.ConfirmReservation(quantity);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();
        await CheckAndPublishStockLevelEvents(item);

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
        await CheckAndPublishStockLevelEvents(item);

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
        await CheckAndPublishStockLevelEvents(item);

        _logger.LogInformation("Added {Quantity} units to product {ProductId}", quantity, productId);
    }

    public async Task RemoveStockAsync(string productId, int quantity)
    {
        var item = await _repository.GetByProductIdAsync(productId)
            ?? throw new KeyNotFoundException($"Inventory item with ProductId {productId} not found");

        item.RemoveStock(quantity);
        await _repository.UpdateAsync(item);
        await _repository.SaveChangesAsync();
        await CheckAndPublishStockLevelEvents(item);

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