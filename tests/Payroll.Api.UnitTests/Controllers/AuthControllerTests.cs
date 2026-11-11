using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Payroll.Api.Controllers;
using Payroll.Application.Abstractions;
using Payroll.Application.Auth;

namespace Payroll.Api.UnitTests.Controllers;

public sealed class AuthControllerTests
{
    private static readonly AuthResult Result = new(
        new LoginResponse("access", new DateTime(2026, 3, 15, 9, 15, 0, DateTimeKind.Utc), ["Hr"], null, "hr@payroll.local"),
        new IssuedRefreshToken("refresh-value", new DateTime(2026, 3, 22, 9, 0, 0, DateTimeKind.Utc)));

    private static (AuthController Controller, IAuthService Service, DefaultHttpContext Http) Create()
    {
        var service = Substitute.For<IAuthService>();
        var http = new DefaultHttpContext();
        var controller = new AuthController(service) { ControllerContext = new ControllerContext { HttpContext = http } };
        return (controller, service, http);
    }

    [Fact]
    public async Task Login_returns_the_access_token_and_sets_a_strict_http_only_refresh_cookie()
    {
        var (controller, service, http) = Create();
        var request = new LoginRequest("hr@payroll.local", "secret");
        service.LoginAsync(request, Arg.Any<CancellationToken>()).Returns(Result);

        var result = await controller.Login(request, TestContext.Current.CancellationToken);

        Assert.Same(Result.Response, Assert.IsType<OkObjectResult>(result.Result).Value);
        var cookie = http.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        Assert.Contains("payroll_refresh=refresh-value", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
        Assert.DoesNotContain("refresh-value", System.Text.Json.JsonSerializer.Serialize(Result.Response));
    }

    [Fact]
    public async Task Refresh_reads_the_cookie()
    {
        var (controller, service, http) = Create();
        http.Request.Headers.Cookie = "payroll_refresh=old-value";
        service.RefreshAsync("old-value", Arg.Any<CancellationToken>()).Returns(Result);

        var result = await controller.Refresh(TestContext.Current.CancellationToken);

        Assert.Same(Result.Response, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Logout_revokes_and_clears_the_cookie()
    {
        var (controller, service, http) = Create();
        http.Request.Headers.Cookie = "payroll_refresh=old-value";

        Assert.IsType<NoContentResult>(await controller.Logout(TestContext.Current.CancellationToken));

        await service.Received(1).LogoutAsync("old-value", Arg.Any<CancellationToken>());
        Assert.Contains("payroll_refresh=;", http.Response.Headers.SetCookie.ToString());
    }
}
