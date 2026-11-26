namespace AttendanceApi.Tenancy;

public interface ITenantContext
{
	Guid? TenantId { get; }

	void Set(Guid tenantId);
}
