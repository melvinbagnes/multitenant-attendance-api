namespace AttendanceApi.Errors;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class TenantViolationException(string message) : Exception(message);
