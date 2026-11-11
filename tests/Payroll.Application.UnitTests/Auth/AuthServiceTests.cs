using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using Payroll.Application.Auth;
using Payroll.Application.Common;
using Payroll.Application.Employees;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Employees;
using Payroll.Domain.Payroll;

namespace Payroll.Application.UnitTests.Auth;

public sealed class AuthServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly FakeCredentialStore _credentials = new();
    private readonly FakeTokenGenerator _tokens = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeTimeProvider _clock = TestData.Clock();
    private readonly EmployeeService _employees;
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _db.Departments.Seed(1, "Engineering");
        _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        var clock = _clock;
        _employees = TestData.EmployeeService(_db, clock);
        _auth = new AuthService(_credentials, _db.Employees, _tokens, _refreshTokens, _db.UnitOfWork, new LoginRequestValidator(),
            new RefreshTokenOptions { RefreshReuseGraceSeconds = 30 }, _clock);
        _credentials.Add("hr@payroll.local", "Hr-Pass1!", Roles.Hr);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> EmployeeUserAsync(string email, EmployeeStatus status = EmployeeStatus.Active)
    {
        var employee = await _employees.CreateAsync(TestData.CreateRequest(email: email, status: status), Ct);
        _credentials.Add(email, "Emp-Pass1!", Roles.Employee, employee.Id);
        return employee.Id;
    }

    [Fact]
    public async Task Hr_user_gets_a_token_without_an_employee_id()
    {
        var response = (await _auth.LoginAsync(new LoginRequest(" hr@payroll.local ", "Hr-Pass1!"), Ct)).Response;

        Assert.Equal(new[] { Roles.Hr }, response.Roles);
        Assert.Null(response.EmployeeId);
        Assert.Equal("token-for-hr@payroll.local", response.AccessToken);
    }

    [Fact]
    public async Task Employee_user_gets_a_token_carrying_their_employee_id()
    {
        var employeeId = await EmployeeUserAsync("ali@demo.local");

        var response = (await _auth.LoginAsync(new LoginRequest("ali@demo.local", "Emp-Pass1!"), Ct)).Response;

        Assert.Equal(new[] { Roles.Employee }, response.Roles);
        Assert.Equal(employeeId, response.EmployeeId);
        Assert.Equal(employeeId, _tokens.LastIssued!.Value.EmployeeId);
    }

    [Theory]
    [InlineData("hr@payroll.local", "wrong")]
    [InlineData("nobody@payroll.local", "Hr-Pass1!")]
    public async Task Wrong_password_or_unknown_email_is_unauthorized(string email, string password)
    {
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.LoginAsync(new LoginRequest(email, password), Ct));

        Assert.Equal(UnauthorizedException.InvalidCredentials, error.Message);
        Assert.Null(_tokens.LastIssued);
    }

    [Fact]
    public async Task Employee_user_whose_employee_is_inactive_is_unauthorized()
    {
        await EmployeeUserAsync("youssef@demo.local", EmployeeStatus.Inactive);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _auth.LoginAsync(new LoginRequest("youssef@demo.local", "Emp-Pass1!"), Ct));
    }

    [Fact]
    public async Task Employee_user_whose_employee_is_deleted_is_unauthorized()
    {
        var employeeId = await EmployeeUserAsync("gone@demo.local");
        await _employees.DeleteAsync(employeeId, Ct);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _auth.LoginAsync(new LoginRequest("gone@demo.local", "Emp-Pass1!"), Ct));
    }

    [Fact]
    public async Task Missing_email_or_password_is_a_validation_error()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => _auth.LoginAsync(new LoginRequest("", ""), Ct));

        Assert.Equal(new[] { "Email", "Password" }, error.Errors.Select(e => e.PropertyName).ToArray());
    }

    private Task<AuthResult> LoginHrAsync() => _auth.LoginAsync(new LoginRequest("hr@payroll.local", "Hr-Pass1!"), Ct);

    [Fact]
    public async Task Login_issues_a_refresh_token()
    {
        var result = await LoginHrAsync();

        Assert.Equal("refresh-1", result.RefreshToken.Value);
        Assert.Single(_refreshTokens.Active);
        Assert.Equal("hr@payroll.local", result.Response.Email);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_issues_a_new_access_token()
    {
        var login = await LoginHrAsync();

        var refreshed = await _auth.RefreshAsync(login.RefreshToken.Value, Ct);

        Assert.NotEqual(login.RefreshToken.Value, refreshed.RefreshToken.Value);
        Assert.Equal("token-for-hr@payroll.local", refreshed.Response.AccessToken);
        Assert.Single(_refreshTokens.Active);
        Assert.Equal("hr@payroll.local", refreshed.Response.Email); // a client can restore its session from the cookie alone
        await _auth.RefreshAsync(refreshed.RefreshToken.Value, Ct); // the new one works
    }

    [Fact]
    public async Task Concurrent_refresh_within_the_grace_window_is_accepted()
    {
        var login = await LoginHrAsync();
        var first = await _auth.RefreshAsync(login.RefreshToken.Value, Ct);   // e.g. the English tab
        _clock.Advance(TimeSpan.FromSeconds(5));

        var second = await _auth.RefreshAsync(login.RefreshToken.Value, Ct);  // the Arabic tab, same cookie

        Assert.Equal(first.Response.Roles, second.Response.Roles);
        Assert.Equal(2, _refreshTokens.Active.Count());
        Assert.DoesNotContain(_refreshTokens.All, t => t.RevokedAtUtc is not null);
    }

    [Fact]
    public async Task A_rotated_token_is_accepted_only_once_inside_the_grace_window()
    {
        var login = await LoginHrAsync();
        await _auth.RefreshAsync(login.RefreshToken.Value, Ct);      // rotation
        await _auth.RefreshAsync(login.RefreshToken.Value, Ct);      // the one extra use (second tab)
        _clock.Advance(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(login.RefreshToken.Value, Ct));

        Assert.All(_refreshTokens.All, t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task User_with_hr_and_employee_roles_signs_in_with_both_roles()
    {
        var employeeId = await EmployeeUserAsync("both@demo.local");
        _credentials.Add("both-user@demo.local", "Both-Pass1!", [Roles.Employee, Roles.Hr], employeeId);

        var response = (await _auth.LoginAsync(new LoginRequest("both-user@demo.local", "Both-Pass1!"), Ct)).Response;

        Assert.Equal(new[] { Roles.Employee, Roles.Hr }, response.Roles);
        Assert.Equal(employeeId, response.EmployeeId);
        Assert.Equal(new[] { Roles.Employee, Roles.Hr }, _tokens.LastIssued!.Value.Roles);
    }

    [Fact]
    public async Task Hr_role_keeps_the_session_even_when_the_linked_employee_is_inactive()
    {
        var employeeId = await EmployeeUserAsync("inactive-hr@demo.local", EmployeeStatus.Inactive);
        _credentials.Add("hr-employee@demo.local", "Both-Pass1!", [Roles.Hr, Roles.Employee], employeeId);

        var response = (await _auth.LoginAsync(new LoginRequest("hr-employee@demo.local", "Both-Pass1!"), Ct)).Response;

        Assert.Contains(Roles.Hr, response.Roles);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_after_the_grace_window_revokes_the_whole_family()
    {
        var login = await LoginHrAsync();
        var refreshed = await _auth.RefreshAsync(login.RefreshToken.Value, Ct);
        _clock.Advance(TimeSpan.FromSeconds(31));

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(login.RefreshToken.Value, Ct));

        Assert.All(_refreshTokens.All, t => Assert.NotNull(t.RevokedAtUtc));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(refreshed.RefreshToken.Value, Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Missing_or_unknown_refresh_token_is_unauthorized(string? token) =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(token, Ct));

    [Fact]
    public async Task Expired_refresh_token_is_unauthorized()
    {
        var login = await LoginHrAsync();
        _clock.Advance(TimeSpan.FromDays(8));

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(login.RefreshToken.Value, Ct));
    }

    [Fact]
    public async Task Employee_deactivated_after_login_cannot_refresh()
    {
        var employeeId = await EmployeeUserAsync("ali@demo.local");
        var login = await _auth.LoginAsync(new LoginRequest("ali@demo.local", "Emp-Pass1!"), Ct);
        var current = await _employees.GetAsync(employeeId, Ct);
        await _employees.UpdateAsync(employeeId, TestData.UpdateRequest(current) with { Status = EmployeeStatus.Inactive }, Ct);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(login.RefreshToken.Value, Ct));
        Assert.All(_refreshTokens.All, t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_ignores_unknown_tokens()
    {
        var login = await LoginHrAsync();

        await _auth.LogoutAsync(login.RefreshToken.Value, Ct);
        await _auth.LogoutAsync("unknown", Ct);
        await _auth.LogoutAsync(null, Ct);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(login.RefreshToken.Value, Ct));
    }
}
