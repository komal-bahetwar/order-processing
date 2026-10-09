using FluentValidation;
using OrderProcessing.Application.Dtos;
using OrderProcessing.Domain;

namespace OrderProcessing.Application.Validation;

public sealed class UpdateStatusRequestValidator : AbstractValidator<UpdateStatusRequest>
{
    public UpdateStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("A target status is required.")
            .Must(status => OrderStatusNames.TryParse(status, out _))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The target status is not a known order status.");
    }
}
