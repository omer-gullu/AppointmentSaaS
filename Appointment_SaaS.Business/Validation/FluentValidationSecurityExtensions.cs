using Appointment_SaaS.Core.Utilities;
using FluentValidation;

namespace Appointment_SaaS.Business.Validation;

public static class FluentValidationSecurityExtensions
{
    public static IRuleBuilderOptions<T, string?> MustBeSafeText<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        int maxLength = 500)
    {
        return ruleBuilder
            .MaximumLength(maxLength)
            .Must(v => string.IsNullOrEmpty(v) || !HtmlInputSanitizer.ContainsXss(v))
            .WithMessage("Geçersiz HTML veya script içeriği tespit edildi.")
            .Must(v => string.IsNullOrEmpty(v) || !HtmlInputSanitizer.ContainsSqlInjection(v))
            .WithMessage("Güvenlik ihlali tespit edildi.");
    }

    public static IRuleBuilderOptions<T, string?> MustBeSafeName<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        int minLength = 2,
        int maxLength = 150)
    {
        return ruleBuilder
            .MinimumLength(minLength)
            .MaximumLength(maxLength)
            .Must(v => string.IsNullOrEmpty(v) || !HtmlInputSanitizer.ContainsXss(v))
            .WithMessage("İsim alanında geçersiz karakterler tespit edildi.")
            .Must(v => string.IsNullOrEmpty(v) || !HtmlInputSanitizer.ContainsSqlInjection(v))
            .WithMessage("İsim alanında güvenlik ihlali tespit edildi.");
    }
}
