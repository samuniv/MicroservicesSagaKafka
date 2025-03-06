using AutoMapper;
using OrderService.Domain.Entities;
using OrderService.Models.DTOs;

namespace OrderService.Infrastructure.Mapping;

public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<Order, OrderResponseDto>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));

        CreateMap<OrderItem, OrderItemDto>();

        CreateMap<Order, OrderStatusDto>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.LastUpdated, opt => opt.MapFrom(src => src.UpdatedAt));

        CreateMap<CreateOrderItemDto, OrderItem>()
            .ConstructUsing((src, ctx) => new OrderItem(src.ProductId, src.Quantity, src.Price));
    }
} 