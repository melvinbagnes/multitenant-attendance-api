namespace AttendanceApi.Domain;

public interface ITenantScoped
{
	Guid TenantId { get; set; }
}
