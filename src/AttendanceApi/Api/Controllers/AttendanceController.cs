using AttendanceApi.Api.Contracts;
using AttendanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

[ApiController]
[Route("api/attendance")]
public sealed class AttendanceController(IAttendanceService attendance) : ControllerBase
{
	[HttpPost("clock-in")]
	public async Task<ActionResult<AttendanceResponse>> ClockIn(ClockInRequest request, CancellationToken ct)
	{
		var record = await attendance.ClockInAsync(request.EmployeeId, request.Note, ct);

		return Ok(AttendanceResponse.From(record));
	}

	[HttpPost("clock-out")]
	public async Task<ActionResult<AttendanceResponse>> ClockOut(ClockOutRequest request, CancellationToken ct)
	{
		var record = await attendance.ClockOutAsync(request.EmployeeId, ct);

		return Ok(AttendanceResponse.From(record));
	}

	[HttpGet]
	public async Task<ActionResult<IEnumerable<AttendanceResponse>>> List(
		[FromQuery] Guid? employeeId,
		[FromQuery] DateOnly? from,
		[FromQuery] DateOnly? to,
		CancellationToken ct)
	{
		var records = await attendance.GetRecordsAsync(employeeId, from, to, ct);

		return Ok(records.Select(AttendanceResponse.From));
	}
}
