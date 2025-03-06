using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OrderService.Domain.Entities;
using OrderService.Domain.Repositories;
using OrderService.Events.IntegrationEvents;
using OrderService.Infrastructure.MessageBus;
using OrderService.Infrastructure.RateLimiting;
using OrderService.Models.DTOs;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly KafkaProducerService _kafkaProducer;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        IOrderRepository orderRepository,
        KafkaProducerService kafkaProducer,
        IMapper mapper,
        ILogger<OrderController> logger)
    {
        _orderRepository = orderRepository;
        _kafkaProducer = kafkaProducer;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order
    /// </summary>
    /// <param name="createOrderDto">Order creation details</param>
    /// <returns>The created order</returns>
    /// <response code="201">Order created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="429">Too many requests</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [RateLimiting("Create")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto)
    {
        try
        {
            _logger.LogInformation("Creating new order for customer {CustomerId} with {ItemCount} items", 
                createOrderDto.CustomerId, 
                createOrderDto.Items?.Count ?? 0);

            if (createOrderDto.Items == null || !createOrderDto.Items.Any())
            {
                _logger.LogWarning("Order creation failed: No items in order for customer {CustomerId}", 
                    createOrderDto.CustomerId);
                return BadRequest("Order must contain at least one item");
            }

            var order = new Order(createOrderDto.CustomerId);
            
            foreach (var item in createOrderDto.Items)
            {
                order.AddItem(new OrderItem(item.ProductId, item.Quantity, item.Price));
            }

            var createdOrder = await _orderRepository.CreateAsync(order);
            
            // Publish the OrderCreated event
            var orderCreatedEvent = new OrderCreatedIntegrationEvent(createdOrder);
            await _kafkaProducer.PublishOrderEventAsync(orderCreatedEvent);
            
            _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId} with total amount {TotalAmount}", 
                createdOrder.Id, 
                createdOrder.CustomerId, 
                createdOrder.TotalAmount);

            var responseDto = _mapper.Map<OrderResponseDto>(createdOrder);
            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", createOrderDto.CustomerId);
            return StatusCode(500, "An error occurred while creating the order");
        }
    }

    /// <summary>
    /// Retrieves an order by ID
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>The requested order</returns>
    /// <response code="200">Order found</response>
    /// <response code="404">Order not found</response>
    /// <response code="429">Too many requests</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}")]
    [RateLimiting("Get")]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(Guid id)
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

            var responseDto = _mapper.Map<OrderResponseDto>(order);
            return Ok(responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the order");
        }
    }

    /// <summary>
    /// Retrieves all orders
    /// </summary>
    /// <returns>List of all orders</returns>
    /// <response code="200">Orders retrieved successfully</response>
    /// <response code="429">Too many requests</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [RateLimiting("List")]
    [ResponseCache(Duration = 30)]
    [ProducesResponseType(typeof(IEnumerable<OrderResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetAllOrders()
    {
        try
        {
            _logger.LogInformation("Retrieving all orders");
            var orders = await _orderRepository.GetAllAsync();
            var responseDtos = _mapper.Map<IEnumerable<OrderResponseDto>>(orders);
            _logger.LogInformation("Retrieved {OrderCount} orders", orders.Count());
            return Ok(responseDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all orders");
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    /// <summary>
    /// Cancels an order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>The cancelled order</returns>
    /// <response code="200">Order cancelled successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="404">Order not found</response>
    /// <response code="429">Too many requests</response>
    /// <response code="500">Internal server error</response>
    [HttpPut("{id}/cancel")]
    [RateLimiting("Cancel")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponseDto>> CancelOrder(Guid id)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for cancellation", id);
                return NotFound();
            }

            try
            {
                order.UpdateStatus(OrderStatus.Cancelled);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot cancel order {OrderId} in status {Status}", id, order.Status);
                return BadRequest($"Cannot cancel order in status {order.Status}");
            }

            await _orderRepository.UpdateAsync(order);

            // Publish the OrderCancelled event
            var orderCancelledEvent = new OrderCancelledIntegrationEvent(order);
            await _kafkaProducer.PublishOrderEventAsync(orderCancelledEvent);

            _logger.LogInformation("Order {OrderId} cancelled successfully", id);
            var responseDto = _mapper.Map<OrderResponseDto>(order);
            return Ok(responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", id);
            return StatusCode(500, "An error occurred while cancelling the order");
        }
    }

    /// <summary>
    /// Gets the current status of an order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>The order status</returns>
    /// <response code="200">Status retrieved successfully</response>
    /// <response code="404">Order not found</response>
    /// <response code="429">Too many requests</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}/status")]
    [RateLimiting("Get")]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "id" })]
    [ProducesResponseType(typeof(OrderStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderStatusDto>> GetOrderStatus(Guid id)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", id);
                return NotFound();
            }

            var statusDto = _mapper.Map<OrderStatusDto>(order);
            return Ok(statusDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for order {OrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the order status");
        }
    }
} 