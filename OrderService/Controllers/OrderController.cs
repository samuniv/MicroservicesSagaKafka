using Microsoft.AspNetCore.Mvc;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using Serilog;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderRepository orderRepository, ILogger<OrderController> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new order for customer {CustomerId} with {ItemCount} items", 
                request.CustomerId, 
                request.Items?.Count ?? 0);

            if (request.Items == null || !request.Items.Any())
            {
                _logger.LogWarning("Order creation failed: No items in order for customer {CustomerId}", 
                    request.CustomerId);
                return BadRequest("Order must contain at least one item");
            }

            var order = new Order(request.CustomerId);
            
            foreach (var item in request.Items)
            {
                order.AddItem(new OrderItem(item.ProductId, item.Quantity, item.Price));
            }

            var createdOrder = await _orderRepository.CreateAsync(order);
            
            _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId} with total amount {TotalAmount}", 
                createdOrder.Id, 
                createdOrder.CustomerId, 
                createdOrder.TotalAmount);

            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", request.CustomerId);
            throw;
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        try
        {
            _logger.LogInformation("Retrieving order {OrderId}", id);
            var order = await _orderRepository.GetByIdAsync(id);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", id);
                return NotFound();
            }

            _logger.LogInformation("Retrieved order {OrderId} for customer {CustomerId}", 
                order.Id, 
                order.CustomerId);
            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", id);
            throw;
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
    {
        try
        {
            _logger.LogInformation("Retrieving all orders");
            var orders = await _orderRepository.GetAllAsync();
            _logger.LogInformation("Retrieved {OrderCount} orders", orders.Count());
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all orders");
            throw;
        }
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByCustomer(string customerId)
    {
        try
        {
            _logger.LogInformation("Retrieving orders for customer {CustomerId}", customerId);
            if (string.IsNullOrWhiteSpace(customerId))
            {
                _logger.LogWarning("CustomerId cannot be empty");
                return BadRequest("CustomerId cannot be empty");
            }

            var orders = await _orderRepository.GetByCustomerIdAsync(customerId);
            _logger.LogInformation("Retrieved {OrderCount} orders for customer {CustomerId}", 
                orders.Count(), 
                customerId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for customer {CustomerId}", customerId);
            throw;
        }
    }
}

public record CreateOrderRequest
{
    public required string CustomerId { get; init; }
    public required List<OrderItemRequest> Items { get; init; }
}

public record OrderItemRequest
{
    public required Guid ProductId { get; init; }
    public required int Quantity { get; init; }
    public required decimal Price { get; init; }
} 