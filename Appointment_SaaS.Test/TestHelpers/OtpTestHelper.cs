using Appointment_SaaS.Core.Utilities;

namespace Appointment_SaaS.Test.TestHelpers;

internal static class OtpTestHelper
{
    public static string Normalize(string? phone) => OtpPhoneNormalizer.Normalize(phone);
}
