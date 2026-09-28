using Appointment_SaaS.Core.Utilities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class AppointmentSlotDurationTests
{
    private static readonly AppointmentSlotDuration.CatalogItem[] Catalog =
    {
        new(10, 5),
        new(11, 10),
        new(12, 30)
    };

    [Fact]
    public void Resolve_ServiceIdsWinOverThirty()
    {
        var result = AppointmentSlotDuration.Resolve(30, null, "10,11", Catalog);
        result.Error.Should().BeNull();
        result.Minutes.Should().Be(15);
        result.Source.Should().Be("serviceIds");
    }

    [Fact]
    public void Resolve_MissingDurationAndServices_HasError()
    {
        var result = AppointmentSlotDuration.Resolve(null, null, null, Catalog);
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Minutes.Should().Be(0);
        result.Source.Should().Be("none");
    }

    [Fact]
    public void Resolve_QueryDurationWithoutServiceIds()
    {
        var result = AppointmentSlotDuration.Resolve(15, null, null, Catalog);
        result.Error.Should().BeNull();
        result.Minutes.Should().Be(15);
        result.Source.Should().Be("durationMinutes");
    }

    [Fact]
    public void Resolve_DoesNotFallBackToThirty()
    {
        var result = AppointmentSlotDuration.Resolve(0, null, null, Catalog);
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Minutes.Should().Be(0);
    }
}
