using AttendanceApi.Api.Contracts;
using AttendanceApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AttendanceApi.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IAttendanceService attendance, TimeProvider clock, IOptions<AttendanceOptions> options) : ControllerBase
{
	/// <summary>Daily hours per employee (regular vs overtime). Defaults to today (UTC).</summary>
	[HttpGet("daily")]
	public async Task<ActionResult<DailySummaryResponse>> Daily([FromQuery] DateOnly? date, CancellationToken ct)
	{
		var day = date ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
		var items = await attendance.GetDailySummaryAsync(day, ct);

		return Ok(new DailySummaryResponse(day, options.Value.RegularHoursPerDay, items));
	}
}
