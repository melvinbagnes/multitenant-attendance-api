namespace AttendanceApi.Tenancy;

public static class TenantClaims
{
	public const string TenantId = "tenant_id";
}

/// <summary>
/// Resolves the tenant from the authenticated user's "tenant_id" claim only.
/// A client-supplied header is never trusted for tenant selection.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
	{
		var claim = context.User.FindFirst(TenantClaims.TenantId)?.Value;

		if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
		{
			tenant.Set(tenantId);
		}

		await next(context);
	}
}
