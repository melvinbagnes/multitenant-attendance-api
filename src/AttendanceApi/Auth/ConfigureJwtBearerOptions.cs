using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceApi.Auth;

public static class AuthPolicies
{
	public const string TenantUser = "TenantUser";
}

/// <summary>
/// Configures JWT validation from <see cref="JwtOptions"/> lazily, so configuration
/// overrides (environment variables, test hosts) are always honoured.
/// </summary>
public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions) : IConfigureNamedOptions<JwtBearerOptions>
{
	public void Configure(string? name, JwtBearerOptions options)
	{
		if (name != JwtBearerDefaults.AuthenticationScheme)
		{
			return;
		}

		var jwt = jwtOptions.Value;

		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwt.Issuer,
			ValidateAudience = true,
			ValidAudience = jwt.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(1)
		};
	}

	public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
