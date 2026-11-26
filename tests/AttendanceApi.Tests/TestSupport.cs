using AttendanceApi.Data;
using AttendanceApi.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Tests;

/// <summary>Controllable clock for deterministic attendance tests.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
	public DateTimeOffset Now { get; set; } = now;

	public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// One shared in-memory SQLite database; each call to <see cref="CreateContext"/> gets
/// a fresh DbContext bound to the given tenant (or to no tenant at all).
/// </summary>
public sealed class TestDatabase : IDisposable
{
	private readonly SqliteConnection _connection = new("DataSource=:memory:");

	public TestDatabase()
	{
		_connection.Open();

		using var context = CreateContext(Guid.NewGuid());
		context.Database.EnsureCreated();
	}

	public AppDbContext CreateContext(Guid? tenantId)
	{
		var tenant = new TenantContext();

		if (tenantId.HasValue)
		{
			tenant.Set(tenantId.Value);
		}

		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseSqlite(_connection)
			.Options;

		return new AppDbContext(options, tenant);
	}

	public void Dispose() => _connection.Dispose();
}
