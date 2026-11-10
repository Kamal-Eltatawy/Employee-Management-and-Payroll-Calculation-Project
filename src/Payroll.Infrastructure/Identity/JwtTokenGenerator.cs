using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Payroll.Application.Abstractions;

namespace Payroll.Infrastructure.Identity;

/// <summary>HS256 access tokens, 15 minutes by default; renewed through the refresh token (§12, D55).</summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock) : ITokenGenerator
{
    public const string RoleClaim = "role";
    public const string EmployeeIdClaim = "employee_id";

    public AccessToken Generate(int userId, string email, string role, int? employeeId)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.LifetimeMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = userId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [JwtRegisteredClaimNames.Email] = email,
            [RoleClaim] = role,
        };
        if (employeeId is { } id)
        {
            claims[EmployeeIdClaim] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(CreateSigningKey(settings.SigningKey), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }

    public static SymmetricSecurityKey CreateSigningKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}
