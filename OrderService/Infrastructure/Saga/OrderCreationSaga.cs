using OrderService.Domain.Entities;
using OrderService.Events.IntegrationEvents;
using OrderService.Infrastructure.MessageBus;
using OrderService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace OrderService.Infrastructure.Saga;

public class OrderCreationSaga
{
    private readonly IOrderRepository _orderRepository;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly ILogger<OrderCreationSaga> _logger;

    public OrderCreationSaga(
        IOrderRepository orderRepository,
        KafkaProducerService kafkaProducer,
        ILogger<OrderCreationSaga> logger)
    {
        _orderRepository = orderRepository;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task StartAsync(Order order)
    {
        try
        {
            _logger.LogInformation("Starting OrderCreation saga for OrderId: {OrderId}", order.Id);

            // Step 1: Reserve Inventory
            await RequestInventoryReservation(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting OrderCreation saga for OrderId: {OrderId}", order.Id);
            await CompensateAndFailOrder(order, "Failed to start saga: " + ex.Message);
            throw;
        }
    }

    public async Task HandleInventoryReservedEvent(InventoryReservedEvent @event)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(@event.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for InventoryReservedEvent. OrderId: {OrderId}", @event.OrderId);
                return;
            }

            order.UpdateStatus(OrderStatus.InventoryReserved);
            await _orderRepository.UpdateAsync(order);

            // Step 2: Process Payment
            await RequestPaymentProcessing(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling InventoryReservedEvent for OrderId: {OrderId}", @event.OrderId);
            var order = await _orderRepository.GetByIdAsync(@event.OrderId);
            if (order != null)
            {
                await CompensateAndFailOrder(order, "Failed to process inventory reservation: " + ex.Message);
            }
        }
    }

    public async Task HandleInventoryReservationFailedEvent(InventoryReservationFailedEvent @event)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(@event.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for InventoryReservationFailedEvent. OrderId: {OrderId}", @event.OrderId);
                return;
            }

            await CompensateAndFailOrder(order, @event.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling InventoryReservationFailedEvent for OrderId: {OrderId}", @event.OrderId);
        }
    }

    public async Task HandlePaymentCompletedEvent(PaymentCompletedEvent @event)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(@event.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for PaymentCompletedEvent. OrderId: {OrderId}", @event.OrderId);
                return;
            }

            order.UpdateStatus(OrderStatus.Completed);
            await _orderRepository.UpdateAsync(order);

            // Publish order completed event
            await _kafkaProducer.PublishOrderEventAsync(new OrderCompletedIntegrationEvent(
                order.Id,
                DateTime.UtcNow,
                order.TotalAmount,
                @event.TransactionId));

            _logger.LogInformation("OrderCreation saga completed successfully for OrderId: {OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PaymentCompletedEvent for OrderId: {OrderId}", @event.OrderId);
            var order = await _orderRepository.GetByIdAsync(@event.OrderId);
            if (order != null)
            {
                await CompensateAndFailOrder(order, "Failed to complete order: " + ex.Message);
            }
        }
    }

    public async Task HandlePaymentFailedEvent(PaymentFailedEvent @event)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(@event.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for PaymentFailedEvent. OrderId: {OrderId}", @event.OrderId);
                return;
            }

            await CompensateAndFailOrder(order, @event.FailureReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PaymentFailedEvent for OrderId: {OrderId}", @event.OrderId);
        }
    }

    private async Task RequestInventoryReservation(Order order)
    {
        var reservationEvent = new RequestInventoryReservationEvent(order);
        await _kafkaProducer.PublishOrderEventAsync(reservationEvent);
        _logger.LogInformation("Published inventory reservation request for OrderId: {OrderId}", order.Id);
    }

    private async Task RequestPaymentProcessing(Order order)
    {
        var paymentEvent = new RequestPaymentProcessingEvent(order);
        await _kafkaProducer.PublishOrderEventAsync(paymentEvent);
        _logger.LogInformation("Published payment processing request for OrderId: {OrderId}", order.Id);
    }

    private async Task CompensateAndFailOrder(Order order, string reason)
    {
        try
        {
            // Update order status to Failed
            order.UpdateStatus(OrderStatus.Failed);
            await _orderRepository.UpdateAsync(order);

            // Release inventory if it was reserved
            if (order.Status == OrderStatus.InventoryReserved)
            {
                var releaseEvent = new ReleaseInventoryEvent(order);
                await _kafkaProducer.PublishOrderEventAsync(releaseEvent);
                _logger.LogInformation("Published inventory release request for failed OrderId: {OrderId}", order.Id);
            }

            // Publish order failed event
            var failedEvent = new OrderFailedIntegrationEvent(order, reason);
            await _kafkaProducer.PublishOrderEventAsync(failedEvent);
            _logger.LogInformation("Published order failed event for OrderId: {OrderId}, Reason: {Reason}", order.Id, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compensation for OrderId: {OrderId}", order.Id);
            throw;
        }
    }
} 