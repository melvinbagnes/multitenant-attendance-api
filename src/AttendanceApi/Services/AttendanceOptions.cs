namespace AttendanceApi.Services;

public sealed class AttendanceOptions
{
	public const string SectionName = "Attendance";

	public decimal RegularHoursPerDay { get; set; } = 8m;
}
