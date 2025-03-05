using FluentValidation;
using InventoryService.Controllers;

namespace InventoryService.Validators;

public class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("ProductId is required and must not exceed 50 characters");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Name is required and must not exceed 100 characters");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Quantity must be greater than or equal to 0");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .WithMessage("Unit price must be greater than 0");

        RuleFor(x => x.SKU)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("SKU is required and must not exceed 50 characters");
    }
}

public class ReserveStockRequestValidator : AbstractValidator<ReserveStockRequest>
{
    public ReserveStockRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");
    }
}

public class UpdateStockRequestValidator : AbstractValidator<UpdateStockRequest>
{
    public UpdateStockRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");
    }
}

public class UpdatePriceRequestValidator : AbstractValidator<UpdatePriceRequest>
{
    public UpdatePriceRequestValidator()
    {
        RuleFor(x => x.NewPrice)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0");
    }
} 