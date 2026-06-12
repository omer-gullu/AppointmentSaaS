namespace Appointment_SaaS.Core.DTOs;

public class ChangePlanInitRequestDto
{
    public string TargetPlanType { get; set; } = string.Empty;
    public string TargetBillingCycle { get; set; } = "Monthly";
    public string? PaymentCallbackUrl { get; set; }
}

public class ChangePlanInitResponseDto
{
    public string Mode { get; set; } = "checkout";
    public string? CheckoutFormContent { get; set; }
    public string? PendingToken { get; set; }
    public string Message { get; set; } = string.Empty;
}
