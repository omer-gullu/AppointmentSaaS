using Appointment_SaaS.Core.Utilities;
using Xunit;

namespace Appointment_SaaS.Test;

public class TurkishTextNormalizerTests
{
    [Fact]
    public void ToTurkishTitleCase_PreservesTurkishCharacters()
    {
        var result = TurkishTextNormalizer.ToTurkishTitleCase("İBRAHİM ÖZTÜRK");
        Assert.Equal("İbrahim Öztürk", result);
    }

    [Fact]
    public void SplitTurkishFullName_SplitsLastWordAsSurname()
    {
        var parts = TurkishTextNormalizer.SplitTurkishFullName("Mehmet Ali Yılmaz");
        Assert.NotNull(parts);
        Assert.Equal("MEHMET ALİ", parts.Value.Ad);
        Assert.Equal("YILMAZ", parts.Value.Soyad);
    }

    [Fact]
    public void SplitTurkishFullName_RejectsSingleWord()
    {
        Assert.Null(TurkishTextNormalizer.SplitTurkishFullName("Ahmet"));
    }
}
