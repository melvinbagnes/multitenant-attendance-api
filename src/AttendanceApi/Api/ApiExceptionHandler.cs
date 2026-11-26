using AttendanceApi.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api;

/// <summary>Maps domain exceptions to RFC 7807 problem responses; anything else falls through to the default 500.</summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		var (status, title) = exception switch
		{
			NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
			ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
			TenantViolationException => (StatusCodes.Status403Forbidden, "Forbidden"),
			_ => (0, string.Empty)
		};

		if (status == 0)
		{
			return false;
		}

		httpContext.Response.StatusCode = status;

		await httpContext.Response.WriteAsJsonAsync(
			new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
			cancellationToken);

		return true;
	}
}
