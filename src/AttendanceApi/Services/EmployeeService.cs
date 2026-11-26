using AttendanceApi.Data;
using AttendanceApi.Domain;
using AttendanceApi.Errors;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Services;

public sealed class EmployeeService(AppDbContext db) : IEmployeeService
{
	public async Task<Employee> CreateAsync(string employeeNumber, string fullName, CancellationToken ct)
	{
		var number = employeeNumber.Trim();

		if (await db.Employees.AnyAsync(e => e.EmployeeNumber == number, ct))
		{
			throw new ConflictException($"Employee number '{number}' already exists.");
		}

		var employee = new Employee
		{
			Id = Guid.NewGuid(),
			EmployeeNumber = number,
			FullName = fullName.Trim()
		};

		db.Employees.Add(employee);

		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException)
		{
			// Lost a race against the unique (TenantId, EmployeeNumber) index.
			throw new ConflictException($"Employee number '{number}' already exists.");
		}

		return employee;
	}

	public async Task<IReadOnlyList<Employee>> ListAsync(CancellationToken ct)
	{
		return await db.Employees
			.AsNoTracking()
			.OrderBy(e => e.EmployeeNumber)
			.ToListAsync(ct);
	}

	public async Task<Employee> GetAsync(Guid id, CancellationToken ct)
	{
		return await db.Employees
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.Id == id, ct)
			?? throw new NotFoundException("Employee not found.");
	}

	public async Task DeactivateAsync(Guid id, CancellationToken ct)
	{
		var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
			?? throw new NotFoundException("Employee not found.");

		employee.IsActive = false;
		await db.SaveChangesAsync(ct);
	}
}
