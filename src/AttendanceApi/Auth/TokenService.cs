using System.Security.Claims;
using System.Text;
using AttendanceApi.Tenancy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceApi.Auth;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
	public string Create(Guid tenantId, string userName)
	{
		var jwt = options.Value;
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
		var now = clock.GetUtcNow().UtcDateTime;

		var descriptor = new SecurityTokenDescriptor
		{
			Issuer = jwt.Issuer,
			Audience = jwt.Audience,
			Subject = new ClaimsIdentity(new[]
			{
				new Claim("sub", userName),
				new Claim(TenantClaims.TenantId, tenantId.ToString())
			}),
			NotBefore = now,
			Expires = now.AddMinutes(jwt.ExpiryMinutes),
			SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
		};

		return new JsonWebTokenHandler().CreateToken(descriptor);
	}
}
