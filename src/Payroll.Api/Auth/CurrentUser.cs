using System.Globalization;
using System.Security.Claims;
using Payroll.Application.Abstractions;
using Payroll.Infrastructure.Identity;

namespace Payroll.Api.Auth;

/// <summary>ICurrentUser from the validated JWT claims (S7).</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public int? EmployeeId =>
        int.TryParse(Principal?.FindFirst(JwtTokenGenerator.EmployeeIdClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}
