using AttendanceApi.Domain;
using AttendanceApi.Errors;
using AttendanceApi.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
	public DbSet<Employee> Employees => Set<Employee>();

	public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

	// No tenant resolved => Guid.Empty => the query filters match nothing (fail closed).
	private Guid CurrentTenantId => tenant.TenantId ?? Guid.Empty;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Employee>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.EmployeeNumber).HasMaxLength(32).IsRequired();
			entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
			entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber }).IsUnique();
			entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
		});

		modelBuilder.Entity<AttendanceRecord>(entity =>
		{
			entity.HasKey(a => a.Id);
			entity.Property(a => a.Note).HasMaxLength(500);
			entity.HasOne(a => a.Employee)
				.WithMany()
				.HasForeignKey(a => a.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);
			entity.HasIndex(a => new { a.TenantId, a.EmployeeId, a.ClockIn });

			// DB-level guarantee: at most one open (not clocked-out) record per employee,
			// even if two clock-in requests race each other.
			entity.HasIndex(a => new { a.TenantId, a.EmployeeId })
				.IsUnique()
				.HasFilter("\"ClockOut\" IS NULL")
				.HasDatabaseName("UX_AttendanceRecords_OneOpenPerEmployee");

			entity.HasQueryFilter(a => a.TenantId == CurrentTenantId);
		});
	}

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		EnforceTenantOwnership();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		EnforceTenantOwnership();
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

	/// <summary>
	/// Stamps the current tenant on new rows and refuses any write that touches another tenant's data.
	/// </summary>
	private void EnforceTenantOwnership()
	{
		foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
		{
			if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
			{
				continue;
			}

			var currentTenantId = tenant.TenantId
				?? throw new TenantViolationException("No tenant is resolved for this request.");

			if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
			{
				entry.Entity.TenantId = currentTenantId;
			}
			else if (entry.Entity.TenantId != currentTenantId)
			{
				throw new TenantViolationException("Cross-tenant write rejected.");
			}
		}
	}
}
