using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryService.Services;
using InventoryService.Domain.Models;
using System.Net;

namespace InventoryService.Controllers;

/// <summary>
/// Controller for managing inventory items and stock levels
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    /// <summary>
    /// Initializes a new instance of the InventoryController
    /// </summary>
    /// <param name="inventoryService">The inventory service</param>
    /// <param name="logger">The logger instance</param>
    public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all inventory items
    /// </summary>
    /// <returns>A list of all inventory items</returns>
    /// <response code="200">Returns the list of inventory items</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user does not have required permissions</response>
    [HttpGet]
    [Authorize(Policy = "InventoryRead")]
    [ProducesResponseType(typeof(IEnumerable<InventoryItem>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAllInventory()
    {
        var items = await _inventoryService.GetAllInventoryItemsAsync();
        return Ok(items);
    }

    /// <summary>
    /// Retrieves an inventory item by its ID
    /// </summary>
    /// <param name="id">The ID of the inventory item</param>
    /// <returns>The inventory item if found</returns>
    /// <response code="200">Returns the inventory item</response>
    /// <response code="404">If the inventory item is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user does not have required permissions</response>
    [HttpGet("{id}")]
    [Authorize(Policy = "InventoryRead")]
    [ProducesResponseType(typeof(InventoryItem), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult<InventoryItem>> GetById(Guid id)
    {
        var item = await _inventoryService.GetInventoryItemAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    [HttpGet("product/{productId}")]
    public async Task<ActionResult<InventoryItem>> GetByProductId(string productId)
    {
        var item = await _inventoryService.GetInventoryItemByProductIdAsync(productId);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    [HttpGet("low-stock/{threshold:int}")]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetLowStockItems(int threshold)
    {
        var items = await _inventoryService.GetLowStockItemsAsync(threshold);
        return Ok(items);
    }

    /// <summary>
    /// Creates a new inventory item
    /// </summary>
    /// <param name="request">The inventory item creation request</param>
    /// <returns>The created inventory item</returns>
    /// <response code="201">Returns the newly created inventory item</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user does not have required permissions</response>
    /// <response code="429">If the rate limit has been exceeded</response>
    [HttpPost]
    [Authorize(Policy = "InventoryWrite")]
    [ProducesResponseType(typeof(InventoryItem), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.TooManyRequests)]
    public async Task<ActionResult<InventoryItem>> CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
    {
        try
        {
            var item = await _inventoryService.CreateInventoryItemAsync(
                request.ProductId,
                request.Name,
                request.Quantity,
                request.UnitPrice,
                request.SKU);

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reserves stock for a product
    /// </summary>
    /// <param name="request">The stock reservation request</param>
    /// <returns>Success status of the reservation</returns>
    /// <response code="200">If the stock was successfully reserved</response>
    /// <response code="400">If there is insufficient stock or the request is invalid</response>
    /// <response code="404">If the product is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user does not have required permissions</response>
    [HttpPost("reserve")]
    [Authorize(Policy = "InventoryWrite")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<ActionResult> ReserveStock([FromBody] ReserveStockRequest request)
    {
        try
        {
            var success = await _inventoryService.ReserveStockAsync(request.ProductId, request.Quantity);
            if (!success)
            {
                return BadRequest(new { message = "Insufficient stock available" });
            }
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("confirm-reservation")]
    public async Task<ActionResult> ConfirmReservation([FromBody] ReserveStockRequest request)
    {
        try
        {
            await _inventoryService.ConfirmReservationAsync(request.ProductId, request.Quantity);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("cancel-reservation")]
    public async Task<ActionResult> CancelReservation([FromBody] ReserveStockRequest request)
    {
        try
        {
            await _inventoryService.CancelReservationAsync(request.ProductId, request.Quantity);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates the stock level for a product
    /// </summary>
    /// <param name="productId">The ID of the product</param>
    /// <param name="request">The stock update request</param>
    /// <returns>The updated inventory item</returns>
    /// <response code="200">Returns the updated inventory item</response>
    /// <response code="400">If the request is invalid</response>
    /// <response code="404">If the product is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user does not have required permissions</response>
    /// <response code="429">If the rate limit has been exceeded</response>
    [HttpPut("{productId}/stock")]
    [Authorize(Policy = "InventoryWrite")]
    [ProducesResponseType(typeof(InventoryItem), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.TooManyRequests)]
    public async Task<ActionResult<InventoryItem>> UpdateStock(string productId, [FromBody] UpdateStockRequest request)
    {
        try
        {
            await _inventoryService.AddStockAsync(productId, request.Quantity);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{productId}/remove-stock")]
    public async Task<ActionResult> RemoveStock(string productId, [FromBody] UpdateStockRequest request)
    {
        try
        {
            await _inventoryService.RemoveStockAsync(productId, request.Quantity);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{productId}/price")]
    public async Task<ActionResult> UpdatePrice(string productId, [FromBody] UpdatePriceRequest request)
    {
        try
        {
            await _inventoryService.UpdatePriceAsync(productId, request.NewPrice);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteInventoryItem(Guid id)
    {
        try
        {
            await _inventoryService.DeleteInventoryItemAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record CreateInventoryItemRequest(
    string ProductId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string SKU);

public record ReserveStockRequest(
    string ProductId,
    int Quantity);

public record UpdateStockRequest(
    int Quantity);

public record UpdatePriceRequest(
    decimal NewPrice); 