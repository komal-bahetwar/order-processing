using FluentValidation;
using OrderProcessing.Application.Dtos;

namespace OrderProcessing.Application.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotNull()
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("An order must contain at least one item.")
            .Must(items => items is { Count: > 0 })
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("An order must contain at least one item.");

        RuleFor(request => request.Items)
            .Must(items => items!.All(item => item is not null))
            .When(request => request.Items is not null)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("An order item must not be null.");

        RuleForEach(request => request.Items)
            .Custom((item, context) =>
            {
                if (item is null)
                {
                    return;
                }

                if (item.ProductId == Guid.Empty)
                {
                    context.AddFailure("A product id is required.");
                }

                if (item.Quantity <= 0)
                {
                    context.AddFailure("Item quantity must be greater than zero.");
                }

                if (item.UnitPrice < 0)
                {
                    context.AddFailure("Item unit price must not be negative.");
                }
            });
    }
}
