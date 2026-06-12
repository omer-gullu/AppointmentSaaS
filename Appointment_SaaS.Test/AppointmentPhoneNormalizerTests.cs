using Appointment_SaaS.Core.Utilities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class AppointmentPhoneNormalizerTests
{
    [Fact]
    public void NormalizeCore_WhatsAppJidAndNationalProduceSameCore()
    {
        var jid = AppointmentPhoneNormalizer.NormalizeCore("905317239931@s.whatsapp.net");
        var national = AppointmentPhoneNormalizer.NormalizeCore("05317239931");
        jid.Should().Be(national);
        jid.Should().Be("5317239931");
    }

    [Fact]
    public void BuildLookupKeys_IncludesWhatsAppJidVariant()
    {
        var keys = AppointmentPhoneNormalizer.BuildLookupKeys("05317239931");
        keys.Should().Contain("905317239931@s.whatsapp.net");
        keys.Should().Contain("05317239931");
        keys.Should().Contain("5317239931");
    }

    [Fact]
    public void BuildLookupKeys_FromJidInput_IncludesNationalVariants()
    {
        var keys = AppointmentPhoneNormalizer.BuildLookupKeys("905317239931@s.whatsapp.net");
        keys.Should().Contain("905317239931@s.whatsapp.net");
        keys.Should().Contain("05317239931");
    }
}
