namespace AttendanceApi.Tenancy;

public sealed class TenantContext : ITenantContext
{
	public Guid? TenantId { get; private set; }

	public void Set(Guid tenantId)
	{
		if (tenantId == Guid.Empty)
		{
			throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
		}

		TenantId = tenantId;
	}
}
