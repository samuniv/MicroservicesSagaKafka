using System;
using System.Collections.Generic;
using OrderService.Domain.Entities;

namespace OrderService.Models.DTOs;

public record CreateOrderDto(
    string CustomerId,
    List<CreateOrderItemDto> Items
);

public record CreateOrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal Price
);

public record OrderResponseDto(
    Guid Id,
    string CustomerId,
    OrderStatus Status,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemDto> Items,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record OrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal Price,
    decimal Subtotal
);

public record OrderStatusDto(
    Guid OrderId,
    OrderStatus Status,
    DateTime? LastUpdated
); 