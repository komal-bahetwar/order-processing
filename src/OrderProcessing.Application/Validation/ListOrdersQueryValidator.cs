using FluentValidation;
using OrderProcessing.Application.Dtos;

namespace OrderProcessing.Application.Validation;

public sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("Page must be at least 1.");

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("Page size must be at least 1.");
    }
}
