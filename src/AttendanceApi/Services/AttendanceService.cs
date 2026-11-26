using AttendanceApi.Data;
using AttendanceApi.Domain;
using AttendanceApi.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttendanceApi.Services;

public sealed class AttendanceService(AppDbContext db, TimeProvider clock, IOptions<AttendanceOptions> options) : IAttendanceService
{
	private const int MaxRecordsPerQuery = 500;

	public async Task<AttendanceRecord> ClockInAsync(Guid employeeId, string? note, CancellationToken ct)
	{
		var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
			?? throw new NotFoundException("Employee not found.");

		if (!employee.IsActive)
		{
			throw new ConflictException("Employee is inactive.");
		}

		var alreadyClockedIn = await db.AttendanceRecords
			.AnyAsync(a => a.EmployeeId == employeeId && a.ClockOut == null, ct);

		if (alreadyClockedIn)
		{
			throw new ConflictException("Employee is already clocked in.");
		}

		var record = new AttendanceRecord
		{
			Id = Guid.NewGuid(),
			EmployeeId = employeeId,
			ClockIn = clock.GetUtcNow().UtcDateTime,
			Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
		};

		db.AttendanceRecords.Add(record);

		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException)
		{
			// Lost a race against the "one open record per employee" unique index.
			throw new ConflictException("Employee is already clocked in.");
		}

		return record;
	}

	public async Task<AttendanceRecord> ClockOutAsync(Guid employeeId, CancellationToken ct)
	{
		var employeeExists = await db.Employees.AnyAsync(e => e.Id == employeeId, ct);

		if (!employeeExists)
		{
			throw new NotFoundException("Employee not found.");
		}

		var record = await db.AttendanceRecords
			.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.ClockOut == null, ct)
			?? throw new ConflictException("Employee is not clocked in.");

		record.ClockOut = clock.GetUtcNow().UtcDateTime;
		await db.SaveChangesAsync(ct);

		return record;
	}

	public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(Guid? employeeId, DateOnly? from, DateOnly? to, CancellationToken ct)
	{
		var query = db.AttendanceRecords.AsNoTracking().AsQueryable();

		if (employeeId.HasValue)
		{
			var id = employeeId.Value;
			query = query.Where(a => a.EmployeeId == id);
		}

		if (from.HasValue)
		{
			var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
			query = query.Where(a => a.ClockIn >= fromUtc);
		}

		if (to.HasValue)
		{
			var toExclusiveUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
			query = query.Where(a => a.ClockIn < toExclusiveUtc);
		}

		return await query
			.OrderByDescending(a => a.ClockIn)
			.Take(MaxRecordsPerQuery)
			.ToListAsync(ct);
	}

	/// <summary>
	/// Daily totals per employee. A record belongs to the UTC day it was clocked in on;
	/// open records are flagged and excluded from the hours until they are closed.
	/// </summary>
	public async Task<IReadOnlyList<DailySummaryItem>> GetDailySummaryAsync(DateOnly date, CancellationToken ct)
	{
		var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
		var endUtc = startUtc.AddDays(1);

		var rows = await db.AttendanceRecords
			.AsNoTracking()
			.Include(a => a.Employee)
			.Where(a => a.ClockIn >= startUtc && a.ClockIn < endUtc)
			.ToListAsync(ct);

		var threshold = options.Value.RegularHoursPerDay;
		var items = new List<DailySummaryItem>();

		foreach (var group in rows.GroupBy(r => r.EmployeeId))
		{
			var employee = group.First().Employee!;
			var closedSessions = group
				.Where(r => r.ClockOut.HasValue)
				.Select(r => r.ClockOut!.Value - r.ClockIn)
				.ToList();

			var hours = OvertimeCalculator.Calculate(closedSessions, threshold);

			items.Add(new DailySummaryItem(
				employee.Id,
				employee.EmployeeNumber,
				employee.FullName,
				hours.TotalHours,
				hours.RegularHours,
				hours.OvertimeHours,
				group.Any(r => !r.ClockOut.HasValue)));
		}

		return items.OrderBy(i => i.EmployeeNumber, StringComparer.Ordinal).ToList();
	}
}
