using AttendanceApi.Domain;

namespace AttendanceApi.Services;

public interface IEmployeeService
{
	Task<Employee> CreateAsync(string employeeNumber, string fullName, CancellationToken ct);

	Task<IReadOnlyList<Employee>> ListAsync(CancellationToken ct);

	Task<Employee> GetAsync(Guid id, CancellationToken ct);

	Task DeactivateAsync(Guid id, CancellationToken ct);
}
