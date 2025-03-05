using Microsoft.AspNetCore.Mvc;
using InventoryService.Services;
using InventoryService.Domain.Models;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAllInventory()
    {
        var items = await _inventoryService.GetAllInventoryItemsAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
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

    [HttpPost]
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

    [HttpPost("reserve")]
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

    [HttpPost("{productId}/add-stock")]
    public async Task<ActionResult> AddStock(string productId, [FromBody] UpdateStockRequest request)
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