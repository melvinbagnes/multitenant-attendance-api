using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AttendanceApi.Api.Contracts;
using AttendanceApi.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceApi.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
	private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"attendance-tests-{Guid.NewGuid():N}.db");

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");

		builder.ConfigureAppConfiguration((_, configuration) =>
		{
			configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Database:Provider"] = "Sqlite",
				["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
				["Jwt:SigningKey"] = "integration-tests-signing-key-at-least-32-chars"
			});
		});
	}

	public HttpClient CreateClientForTenant(Guid tenantId)
	{
		var token = Services.GetRequiredService<TokenService>().Create(tenantId, "integration-test");
		var client = CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		return client;
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			try
			{
				File.Delete(_databasePath);
			}
			catch (IOException)
			{
				// Best-effort cleanup of the temp database.
			}
		}
	}
}

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
	private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

	private readonly ApiFactory _factory;

	public ApiIntegrationTests(ApiFactory factory) => _factory = factory;

	private static async Task<EmployeeResponse> CreateEmployeeAsync(HttpClient client, string number)
	{
		var response = await client.PostAsJsonAsync("/api/employees", new { employeeNumber = number, fullName = $"Employee {number}" });

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		return (await response.Content.ReadFromJsonAsync<EmployeeResponse>())!;
	}

	[Fact]
	public async Task Health_IsPublic()
	{
		var response = await _factory.CreateClient().GetAsync("/health");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task Api_WithoutAToken_IsUnauthorized()
	{
		var response = await _factory.CreateClient().GetAsync("/api/employees");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task CreateEmployee_WithMissingName_IsABadRequest()
	{
		var client = _factory.CreateClientForTenant(TenantA);

		var response = await client.PostAsJsonAsync("/api/employees", new { employeeNumber = "E-900", fullName = "" });

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task DuplicateEmployeeNumber_IsAConflict()
	{
		var client = _factory.CreateClientForTenant(TenantA);

		await CreateEmployeeAsync(client, "E-DUP");
		var second = await client.PostAsJsonAsync("/api/employees", new { employeeNumber = "E-DUP", fullName = "Someone else" });

		Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
	}

	[Fact]
	public async Task ClockInOutFlow_ProducesADailyReport()
	{
		var client = _factory.CreateClientForTenant(TenantA);
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var employee = await CreateEmployeeAsync(client, "E-FLOW");

		var clockIn = await client.PostAsJsonAsync("/api/attendance/clock-in", new { employeeId = employee.Id });
		var clockInAgain = await client.PostAsJsonAsync("/api/attendance/clock-in", new { employeeId = employee.Id });
		var clockOut = await client.PostAsJsonAsync("/api/attendance/clock-out", new { employeeId = employee.Id });
		var clockOutAgain = await client.PostAsJsonAsync("/api/attendance/clock-out", new { employeeId = employee.Id });

		Assert.Equal(HttpStatusCode.OK, clockIn.StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, clockInAgain.StatusCode);
		Assert.Equal(HttpStatusCode.OK, clockOut.StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, clockOutAgain.StatusCode);

		var report = await client.GetFromJsonAsync<DailySummaryResponse>($"/api/reports/daily?date={today:yyyy-MM-dd}");

		Assert.NotNull(report);
		Assert.Contains(report.Employees, e => e.EmployeeId == employee.Id && !e.HasOpenRecord);
	}

	[Fact]
	public async Task Tenants_CannotSeeEachOthersEmployees()
	{
		var clientA = _factory.CreateClientForTenant(TenantA);
		var clientB = _factory.CreateClientForTenant(TenantB);

		var employeeOfA = await CreateEmployeeAsync(clientA, "E-ISOLATED");

		var directLookup = await clientB.GetAsync($"/api/employees/{employeeOfA.Id}");
		var clockInAttempt = await clientB.PostAsJsonAsync("/api/attendance/clock-in", new { employeeId = employeeOfA.Id });
		var listForB = await clientB.GetFromJsonAsync<List<EmployeeResponse>>("/api/employees");

		Assert.Equal(HttpStatusCode.NotFound, directLookup.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, clockInAttempt.StatusCode);
		Assert.NotNull(listForB);
		Assert.DoesNotContain(listForB, e => e.Id == employeeOfA.Id);
	}
}
