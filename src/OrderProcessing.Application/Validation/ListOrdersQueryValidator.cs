using FluentValidation;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Validation;

public sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        RuleFor(query => query.Limit)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The limit must be at least 1.");

        RuleFor(query => query.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) || OrderStatusNames.TryParse(status, out _))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The status filter is not a known order status.");
    }
}
