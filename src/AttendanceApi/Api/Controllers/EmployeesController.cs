using AttendanceApi.Api.Contracts;
using AttendanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
	[HttpPost]
	public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request, CancellationToken ct)
	{
		var employee = await employees.CreateAsync(request.EmployeeNumber, request.FullName, ct);

		return CreatedAtAction(nameof(Get), new { id = employee.Id }, EmployeeResponse.From(employee));
	}

	[HttpGet]
	public async Task<ActionResult<IEnumerable<EmployeeResponse>>> List(CancellationToken ct)
	{
		var items = await employees.ListAsync(ct);

		return Ok(items.Select(EmployeeResponse.From));
	}

	[HttpGet("{id:guid}")]
	public async Task<ActionResult<EmployeeResponse>> Get(Guid id, CancellationToken ct)
	{
		var employee = await employees.GetAsync(id, ct);

		return Ok(EmployeeResponse.From(employee));
	}

	[HttpPost("{id:guid}/deactivate")]
	public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
	{
		await employees.DeactivateAsync(id, ct);

		return NoContent();
	}
}
