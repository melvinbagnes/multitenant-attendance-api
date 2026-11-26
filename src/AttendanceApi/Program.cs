using AttendanceApi.Api;
using AttendanceApi.Api.Contracts;
using AttendanceApi.Auth;
using AttendanceApi.Data;
using AttendanceApi.Services;
using AttendanceApi.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();

builder.Services.AddOptions<AttendanceOptions>()
	.BindConfiguration(AttendanceOptions.SectionName)
	.Validate(o => o.RegularHoursPerDay is > 0 and <= 24, "Attendance:RegularHoursPerDay must be between 0 and 24.")
	.ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
	.BindConfiguration(JwtOptions.SectionName)
	.Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters.")
	.ValidateOnStart();

builder.Services.AddSingleton<TokenService>();

// Provider and connection string are resolved lazily so environment variables
// (and test hosts) can override them: Database__Provider, ConnectionStrings__Default.
builder.Services.AddDbContext<AppDbContext>((serviceProvider, dbOptions) =>
{
	var configuration = serviceProvider.GetRequiredService<IConfiguration>();
	var connectionString = configuration.GetConnectionString("Default");

	if (string.IsNullOrWhiteSpace(connectionString))
	{
		throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
	}

	if (string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
	{
		dbOptions.UseSqlite(connectionString);
	}
	else
	{
		dbOptions.UseNpgsql(connectionString);
	}
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();

builder.Services.AddAuthorizationBuilder()
	.AddPolicy(AuthPolicies.TenantUser, policy => policy
		.RequireAuthenticatedUser()
		.RequireAssertion(context => Guid.TryParse(context.User.FindFirst(TenantClaims.TenantId)?.Value, out _)));

var app = builder.Build();

if (app.Configuration.GetValue("Database:EnsureCreated", true))
{
	using var scope = app.Services.CreateScope();
	await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers().RequireAuthorization(AuthPolicies.TenantUser);

if (app.Environment.IsDevelopment())
{
	// Demo only: issues a token for any tenant id. Never mapped outside Development.
	app.MapPost("/dev/token", (DevTokenRequest request, TokenService tokens) =>
		Results.Ok(new
		{
			accessToken = tokens.Create(request.TenantId, string.IsNullOrWhiteSpace(request.UserName) ? "dev-user" : request.UserName),
			tenantId = request.TenantId
		}));
}

app.Run();

public partial class Program;
