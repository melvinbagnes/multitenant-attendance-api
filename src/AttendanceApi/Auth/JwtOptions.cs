namespace AttendanceApi.Auth;

public sealed class JwtOptions
{
	public const string SectionName = "Jwt";

	public string SigningKey { get; set; } = string.Empty;

	public string Issuer { get; set; } = "attendance-api";

	public string Audience { get; set; } = "attendance-api-clients";

	public int ExpiryMinutes { get; set; } = 60;
}
