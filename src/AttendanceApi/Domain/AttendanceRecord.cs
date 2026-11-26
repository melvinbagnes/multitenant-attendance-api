namespace AttendanceApi.Domain;

public class AttendanceRecord : ITenantScoped
{
	public Guid Id { get; set; }

	public Guid TenantId { get; set; }

	public Guid EmployeeId { get; set; }

	public Employee? Employee { get; set; }

	/// <summary>UTC clock-in time.</summary>
	public DateTime ClockIn { get; set; }

	/// <summary>UTC clock-out time; null while the employee is still clocked in.</summary>
	public DateTime? ClockOut { get; set; }

	public string? Note { get; set; }
}
