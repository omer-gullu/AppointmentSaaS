using System;

namespace Appointment_SaaS.Core.Constants;

public static class BillingCycles
{
    public const string Monthly = "Monthly";
    public const string Yearly = "Yearly";

    public static bool IsValid(string? cycle) =>
        string.Equals(cycle, Monthly, StringComparison.OrdinalIgnoreCase)
        || string.Equals(cycle, Yearly, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? cycle) =>
        string.Equals(cycle, Yearly, StringComparison.OrdinalIgnoreCase) ? Yearly : Monthly;
}
