using AttendanceApi.Data;
using AttendanceApi.Domain;
using AttendanceApi.Errors;
using AttendanceApi.Services;
using Microsoft.Extensions.Options;

namespace AttendanceApi.Tests;

public sealed class AttendanceServiceTests : IDisposable
{
	private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

	private readonly TestDatabase _database = new();
	private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero));

	public void Dispose() => _database.Dispose();

	private AttendanceService CreateService(AppDbContext context) =>
		new(context, _clock, Options.Create(new AttendanceOptions { RegularHoursPerDay = 8m }));

	private async Task<Guid> SeedEmployeeAsync(Guid tenantId, string number = "E-001", bool isActive = true)
	{
		await using var context = _database.CreateContext(tenantId);
		var employee = new Employee
		{
			Id = Guid.NewGuid(),
			EmployeeNumber = number,
			FullName = $"Employee {number}",
			IsActive = isActive
		};

		context.Employees.Add(employee);
		await context.SaveChangesAsync();

		return employee.Id;
	}

	[Fact]
	public async Task ClockIn_CreatesAnOpenRecord()
	{
		var employeeId = await SeedEmployeeAsync(TenantA);
		await using var context = _database.CreateContext(TenantA);

		var record = await CreateService(context).ClockInAsync(employeeId, "  Morning shift ", CancellationToken.None);

		Assert.Equal(_clock.Now.UtcDateTime, record.ClockIn);
		Assert.Null(record.ClockOut);
		Assert.Equal("Morning shift", record.Note);
		Assert.Equal(TenantA, record.TenantId);
	}

	[Fact]
	public async Task ClockIn_Twice_IsAConflict()
	{
		var employeeId = await SeedEmployeeAsync(TenantA);
		await using var context = _database.CreateContext(TenantA);
		var service = CreateService(context);

		await service.ClockInAsync(employeeId, null, CancellationToken.None);

		await Assert.ThrowsAsync<ConflictException>(
			() => service.ClockInAsync(employeeId, null, CancellationToken.None));
	}

	[Fact]
	public async Task ClockIn_ForUnknownEmployee_IsNotFound()
	{
		await using var context = _database.CreateContext(TenantA);

		await Assert.ThrowsAsync<NotFoundException>(
			() => CreateService(context).ClockInAsync(Guid.NewGuid(), null, CancellationToken.None));
	}

	[Fact]
	public async Task ClockIn_ForInactiveEmployee_IsAConflict()
	{
		var employeeId = await SeedEmployeeAsync(TenantA, isActive: false);
		await using var context = _database.CreateContext(TenantA);

		await Assert.ThrowsAsync<ConflictException>(
			() => CreateService(context).ClockInAsync(employeeId, null, CancellationToken.None));
	}

	[Fact]
	public async Task ClockOut_ClosesTheOpenRecord()
	{
		var employeeId = await SeedEmployeeAsync(TenantA);
		await using var context = _database.CreateContext(TenantA);
		var service = CreateService(context);

		await service.ClockInAsync(employeeId, null, CancellationToken.None);
		_clock.Now = _clock.Now.AddHours(9);
		var record = await service.ClockOutAsync(employeeId, CancellationToken.None);

		Assert.Equal(_clock.Now.UtcDateTime, record.ClockOut);
	}

	[Fact]
	public async Task ClockOut_WithoutClockIn_IsAConflict()
	{
		var employeeId = await SeedEmployeeAsync(TenantA);
		await using var context = _database.CreateContext(TenantA);

		await Assert.ThrowsAsync<ConflictException>(
			() => CreateService(context).ClockOutAsync(employeeId, CancellationToken.None));
	}

	[Fact]
	public async Task AnotherTenant_CannotClockInSomeoneElsesEmployee()
	{
		var employeeOfA = await SeedEmployeeAsync(TenantA);
		await using var contextB = _database.CreateContext(TenantB);

		await Assert.ThrowsAsync<NotFoundException>(
			() => CreateService(contextB).ClockInAsync(employeeOfA, null, CancellationToken.None));
	}

	[Fact]
	public async Task DailySummary_SplitsRegularAndOvertimeHours()
	{
		var employeeId = await SeedEmployeeAsync(TenantA);
		await using var context = _database.CreateContext(TenantA);
		var service = CreateService(context);

		await service.ClockInAsync(employeeId, null, CancellationToken.None);
		_clock.Now = _clock.Now.AddHours(10);
		await service.ClockOutAsync(employeeId, CancellationToken.None);

		var summary = await service.GetDailySummaryAsync(new DateOnly(2026, 3, 2), CancellationToken.None);

		var item = Assert.Single(summary);
		Assert.Equal(10m, item.TotalHours);
		Assert.Equal(8m, item.RegularHours);
		Assert.Equal(2m, item.OvertimeHours);
		Assert.False(item.HasOpenRecord);
	}

	[Fact]
	public async Task DailySummary_FlagsOpenRecords_AndOnlyCountsTheRequestedDay()
	{
		var employeeId = await SeedEmployeeAsync(TenantA);
		await using var context = _database.CreateContext(TenantA);
		var service = CreateService(context);

		await service.ClockInAsync(employeeId, null, CancellationToken.None);

		var sameDay = await service.GetDailySummaryAsync(new DateOnly(2026, 3, 2), CancellationToken.None);
		var otherDay = await service.GetDailySummaryAsync(new DateOnly(2026, 3, 3), CancellationToken.None);

		Assert.True(Assert.Single(sameDay).HasOpenRecord);
		Assert.Empty(otherDay);
	}
}
