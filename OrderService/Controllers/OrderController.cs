using Microsoft.AspNetCore.Mvc;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;

    public OrderController(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest("Order must contain at least one item");
        }

        var order = new Order(request.CustomerId);
        
        foreach (var item in request.Items)
        {
            order.AddItem(new OrderItem(item.ProductId, item.Quantity, item.Price));
        }

        await _orderRepository.CreateAsync(order);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        var orders = await _orderRepository.GetAllAsync();
        return Ok(orders);
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByCustomer(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return BadRequest("CustomerId cannot be empty");
        }

        var orders = await _orderRepository.GetByCustomerIdAsync(customerId);
        return Ok(orders);
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