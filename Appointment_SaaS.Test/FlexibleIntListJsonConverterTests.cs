using System.Text.Json;
using Appointment_SaaS.Core.DTOs;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class FlexibleIntListJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData("{\"serviceIds\":[20,18]}", new[] { 20, 18 })]
    [InlineData("{\"serviceIds\":\"20,18\"}", new[] { 20, 18 })]
    [InlineData("{\"serviceIds\":\"[20, 18]\"}", new[] { 20, 18 })]
    [InlineData("{\"serviceIds\":20}", new[] { 20 })]
    public void ServiceIds_ShouldBindFromArrayOrCsv(string json, int[] expected)
    {
        var dto = JsonSerializer.Deserialize<AppointmentCreateDto>(json, Options);
        dto.Should().NotBeNull();
        dto!.ServiceIds.Should().Equal(expected);
    }
}
