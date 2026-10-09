using FluentValidation;
using OrderProcessing.Application.Dtos;

namespace OrderProcessing.Application.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("An order must contain at least one item.");

        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty()
                .WithErrorCode("VALIDATION_ERROR")
                .WithMessage("A product id is required.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithErrorCode("VALIDATION_ERROR")
                .WithMessage("Item quantity must be greater than zero.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithErrorCode("VALIDATION_ERROR")
                .WithMessage("Item unit price must not be negative.");
        });
    }
}
