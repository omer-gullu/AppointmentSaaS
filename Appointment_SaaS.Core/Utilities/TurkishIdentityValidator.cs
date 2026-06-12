namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// T.C. Kimlik (11 hane) ve Vergi Kimlik (10 hane) numarası algoritma doğrulaması.
/// </summary>
public static class TurkishIdentityValidator
{
    public static bool IsValidTcOrVkn(string? identityNumber)
    {
        if (string.IsNullOrWhiteSpace(identityNumber)) return false;

        if (identityNumber.Length == 11)
            return IsValidTcKimlik(identityNumber);

        if (identityNumber.Length == 10)
            return IsValidVkn(identityNumber);

        return false;
    }

    public static bool IsValidTcKimlik(string tcKimlik)
    {
        if (tcKimlik.Length != 11 || !IsAllDigits(tcKimlik) || tcKimlik[0] == '0')
            return false;

        var digits = tcKimlik.Select(c => c - '0').ToArray();

        var sumOdd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var sumEven = digits[1] + digits[3] + digits[5] + digits[7];

        var digit10 = ((sumOdd * 7) - sumEven) % 10;
        if (digit10 < 0) digit10 += 10;

        var digit11 = digits.Take(10).Sum() % 10;

        return digits[9] == digit10 && digits[10] == digit11;
    }

    public static bool IsValidVkn(string vkn)
    {
        if (vkn.Length != 10 || !IsAllDigits(vkn))
            return false;

        var digits = vkn.Select(c => c - '0').ToArray();
        var sum = 0;

        for (var i = 0; i < 9; i++)
        {
            var tmp = (digits[i] + (9 - i)) % 10;
            if (tmp != 0)
            {
                tmp = (tmp * (int)Math.Pow(2, 9 - i)) % 9;
                if (tmp == 0) tmp = 9;
            }

            sum += tmp;
        }

        var lastDigit = (10 - (sum % 10)) % 10;
        return digits[9] == lastDigit;
    }

    private static bool IsAllDigits(string value)
    {
        foreach (var ch in value)
        {
            if (ch < '0' || ch > '9') return false;
        }

        return true;
    }
}
