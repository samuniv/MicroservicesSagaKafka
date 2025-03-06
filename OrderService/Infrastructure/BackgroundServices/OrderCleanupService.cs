using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.BackgroundServices;

public class OrderCleanupService : BackgroundService
{
    private readonly ILogger<OrderCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);
    private readonly TimeSpan _orderRetentionPeriod = TimeSpan.FromDays(90);

    public OrderCleanupService(
        ILogger<OrderCleanupService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldOrdersAsync(stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred while cleaning up old orders");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task CleanupOldOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var cutoffDate = DateTime.UtcNow.Subtract(_orderRetentionPeriod);
        var ordersToDelete = await dbContext.Orders
            .Where(o => o.CreatedAt < cutoffDate && 
                       (o.Status == OrderStatus.Completed || 
                        o.Status == OrderStatus.Failed || 
                        o.Status == OrderStatus.Cancelled))
            .ToListAsync(cancellationToken);

        if (!ordersToDelete.Any())
        {
            _logger.LogInformation("No orders found for cleanup");
            return;
        }

        _logger.LogInformation(
            "Found {Count} orders older than {RetentionPeriod} days to clean up",
            ordersToDelete.Count,
            _orderRetentionPeriod.TotalDays);

        dbContext.Orders.RemoveRange(ordersToDelete);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully cleaned up {Count} old orders",
            ordersToDelete.Count);
    }
} 