using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Abstractions;
using Payroll.Application.Auth;

namespace Payroll.Api.Controllers;

/// <summary>
/// Login, refresh and logout (D55). The refresh token lives only in an HttpOnly, Secure, SameSite=Strict cookie
/// scoped to /api/auth: JavaScript cannot read it, and the browser sends it nowhere else.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    public const string RefreshCookie = "payroll_refresh";
    private const string CookiePath = "/api/auth";

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(WithCookie(await auth.LoginAsync(request, ct)));

    /// <summary>Uses the refresh cookie; returns a new access token and rotates the cookie.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        // On success the rotated cookie replaces the old one. On failure the exception handler writes the 401;
        // a stale cookie left in the browser is harmless because its token is already revoked or unknown.
        return Ok(WithCookie(await auth.RefreshAsync(Request.Cookies[RefreshCookie], ct)));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        DeleteCookie();
        return NoContent();
    }

    private LoginResponse WithCookie(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookie, result.RefreshToken.Value, CookieOptions(result.RefreshToken));
        return result.Response;
    }

    private void DeleteCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions(expiresWith: null));

    private static CookieOptions CookieOptions(IssuedRefreshToken? expiresWith) => new()
    {
        HttpOnly = true,
        Secure = true, // browsers accept Secure cookies on http://localhost, so this also works in development
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expiresWith is null ? null : new DateTimeOffset(expiresWith.ExpiresAtUtc, TimeSpan.Zero),
        IsEssential = true,
    };
}
