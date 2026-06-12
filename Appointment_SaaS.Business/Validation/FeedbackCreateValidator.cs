using FluentValidation;

namespace Appointment_SaaS.Business.Validation;

public class FeedbackCreateValidator : AbstractValidator<FeedbackCreateRequest>
{
    public FeedbackCreateValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.FeedbackType)
            .NotEmpty()
            .MaximumLength(50)
            .MustBeSafeText(maxLength: 50);
        RuleFor(x => x.Message)
            .NotEmpty()
            .MustBeSafeText(maxLength: 2000);
    }
}

/// <summary>API feedback endpoint doğrulaması için.</summary>
public class FeedbackCreateRequest
{
    public int TenantId { get; set; }
    public string FeedbackType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
