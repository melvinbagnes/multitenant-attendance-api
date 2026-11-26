using AttendanceApi.Domain;
using AttendanceApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Tests;

public sealed class TenantIsolationTests : IDisposable
{
	private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

	private readonly TestDatabase _database = new();

	public void Dispose() => _database.Dispose();

	private static Employee NewEmployee(string number, string name) => new()
	{
		Id = Guid.NewGuid(),
		EmployeeNumber = number,
		FullName = name
	};

	[Fact]
	public async Task Queries_ReturnOnlyTheCurrentTenantsRows()
	{
		await using (var contextA = _database.CreateContext(TenantA))
		{
			contextA.Employees.Add(NewEmployee("E-001", "Alice (A)"));
			await contextA.SaveChangesAsync();
		}

		await using (var contextB = _database.CreateContext(TenantB))
		{
			contextB.Employees.Add(NewEmployee("E-001", "Bob (B)"));
			await contextB.SaveChangesAsync();
		}

		await using var readA = _database.CreateContext(TenantA);
		await using var readB = _database.CreateContext(TenantB);

		var seenByA = await readA.Employees.ToListAsync();
		var seenByB = await readB.Employees.ToListAsync();

		Assert.Equal("Alice (A)", Assert.Single(seenByA).FullName);
		Assert.Equal("Bob (B)", Assert.Single(seenByB).FullName);
	}

	[Fact]
	public async Task WithoutATenant_NothingIsVisible()
	{
		await using (var contextA = _database.CreateContext(TenantA))
		{
			contextA.Employees.Add(NewEmployee("E-001", "Alice"));
			await contextA.SaveChangesAsync();
		}

		await using var anonymous = _database.CreateContext(null);

		Assert.Empty(await anonymous.Employees.ToListAsync());
	}

	[Fact]
	public async Task NewRows_AreStampedWithTheCurrentTenant()
	{
		await using var context = _database.CreateContext(TenantA);
		var employee = NewEmployee("E-001", "Alice");

		context.Employees.Add(employee);
		await context.SaveChangesAsync();

		Assert.Equal(TenantA, employee.TenantId);
	}

	[Fact]
	public async Task WritingAnotherTenantsId_IsRejected()
	{
		await using var context = _database.CreateContext(TenantA);
		var employee = NewEmployee("E-001", "Mallory");
		employee.TenantId = TenantB;

		context.Employees.Add(employee);

		await Assert.ThrowsAsync<TenantViolationException>(() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task SavingWithoutATenant_IsRejected()
	{
		await using var context = _database.CreateContext(null);

		context.Employees.Add(NewEmployee("E-001", "Nobody"));

		await Assert.ThrowsAsync<TenantViolationException>(() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task EmployeeNumber_MustBeUniqueWithinATenant()
	{
		await using var context = _database.CreateContext(TenantA);

		context.Employees.Add(NewEmployee("E-001", "First"));
		context.Employees.Add(NewEmployee("E-001", "Second"));

		await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
	}
}
