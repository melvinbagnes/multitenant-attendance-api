using System.ComponentModel.DataAnnotations;
using AttendanceApi.Domain;
using AttendanceApi.Services;

namespace AttendanceApi.Api.Contracts;

public sealed class CreateEmployeeRequest
{
	[Required, StringLength(32)]
	public string EmployeeNumber { get; init; } = string.Empty;

	[Required, StringLength(200)]
	public string FullName { get; init; } = string.Empty;
}

public sealed class ClockInRequest
{
	[Required]
	public Guid EmployeeId { get; init; }

	[StringLength(500)]
	public string? Note { get; init; }
}

public sealed class ClockOutRequest
{
	[Required]
	public Guid EmployeeId { get; init; }
}

public sealed record DevTokenRequest(Guid TenantId, string? UserName);

public sealed record EmployeeResponse(Guid Id, string EmployeeNumber, string FullName, bool IsActive)
{
	public static EmployeeResponse From(Employee employee) =>
		new(employee.Id, employee.EmployeeNumber, employee.FullName, employee.IsActive);
}

public sealed record AttendanceResponse(
	Guid Id,
	Guid EmployeeId,
	DateTime ClockInUtc,
	DateTime? ClockOutUtc,
	decimal? WorkedHours,
	string? Note)
{
	public static AttendanceResponse From(AttendanceRecord record)
	{
		DateTime? clockOut = record.ClockOut.HasValue ? AsUtc(record.ClockOut.Value) : null;
		decimal? workedHours = record.ClockOut.HasValue
			? Math.Round((decimal)(record.ClockOut.Value - record.ClockIn).TotalHours, 2)
			: null;

		return new AttendanceResponse(record.Id, record.EmployeeId, AsUtc(record.ClockIn), clockOut, workedHours, record.Note);
	}

	private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed record DailySummaryResponse(DateOnly Date, decimal RegularHoursPerDay, IReadOnlyList<DailySummaryItem> Employees);
