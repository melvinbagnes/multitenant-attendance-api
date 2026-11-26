namespace AttendanceApi.Domain;

public class Employee : ITenantScoped
{
	public Guid Id { get; set; }

	public Guid TenantId { get; set; }

	public string EmployeeNumber { get; set; } = string.Empty;

	public string FullName { get; set; } = string.Empty;

	public bool IsActive { get; set; } = true;
}
