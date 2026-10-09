using FluentValidation;
using OrderProcessing.Application.Dtos;

namespace OrderProcessing.Application.Validation;

public sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        RuleFor(query => query.Limit)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The limit must be at least 1.");
    }
}
