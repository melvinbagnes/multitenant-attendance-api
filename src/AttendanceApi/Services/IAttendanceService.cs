using AttendanceApi.Domain;

namespace AttendanceApi.Services;

public sealed record DailySummaryItem(
	Guid EmployeeId,
	string EmployeeNumber,
	string FullName,
	decimal TotalHours,
	decimal RegularHours,
	decimal OvertimeHours,
	bool HasOpenRecord);

public interface IAttendanceService
{
	Task<AttendanceRecord> ClockInAsync(Guid employeeId, string? note, CancellationToken ct);

	Task<AttendanceRecord> ClockOutAsync(Guid employeeId, CancellationToken ct);

	Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken ct);

	Task<IReadOnlyList<DailySummaryItem>> GetDailySummaryAsync(DateOnly date, CancellationToken ct);
}
