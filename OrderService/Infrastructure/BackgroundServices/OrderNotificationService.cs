using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.MessageBus;

namespace OrderService.Infrastructure.BackgroundServices;

public class OrderNotificationService : BackgroundService
{
    private readonly ILogger<OrderNotificationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public OrderNotificationService(
        ILogger<OrderNotificationService> logger,
        IServiceScopeFactory scopeFactory,
        KafkaProducerService kafkaProducer)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _kafkaProducer = kafkaProducer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationsAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred while processing order notifications");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var ordersToNotify = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Completed || 
                       o.Status == OrderStatus.Failed || 
                       o.Status == OrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var order in ordersToNotify)
        {
            try
            {
                var notification = new OrderNotificationEvent(
                    order.Id,
                    order.CustomerId,
                    order.Status,
                    order.TotalAmount,
                    order.UpdatedAt ?? order.CreatedAt);

                await _kafkaProducer.PublishOrderEventAsync(notification);

                _logger.LogInformation(
                    "Sent notification for order {OrderId} with status {Status}",
                    order.Id,
                    order.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send notification for order {OrderId}",
                    order.Id);
            }
        }
    }
}

public record OrderNotificationEvent(
    Guid OrderId,
    string CustomerId,
    OrderStatus Status,
    decimal TotalAmount,
    DateTime Timestamp); 