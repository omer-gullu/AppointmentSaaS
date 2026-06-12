using System.Text.Json;
using Appointment_SaaS.WebUI.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.WebUI.Controllers;

[ApiController]
[Route("__dev/perf")]
public class DevPerfController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromBody] JsonElement body)
    {
        if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            return NotFound();

        var hypothesisId = body.TryGetProperty("hypothesisId", out var h) ? h.GetString() ?? "H-CLIENT" : "H-CLIENT";
        var location = body.TryGetProperty("location", out var l) ? l.GetString() ?? "client" : "client";
        var message = body.TryGetProperty("message", out var m) ? m.GetString() ?? "client_perf" : "client_perf";
        var runId = body.TryGetProperty("runId", out var r) ? r.GetString() ?? "probe" : "probe";

        PerfProbeLog.Write(hypothesisId, location, message, JsonSerializer.Deserialize<object>(body.GetRawText()) ?? new { }, runId);
        return NoContent();
    }
}
