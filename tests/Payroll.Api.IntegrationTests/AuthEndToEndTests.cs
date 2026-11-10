using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Payroll.Api.IntegrationTests;

public sealed class AuthEndToEndTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_is_anonymous()
    {
        var response = await api.CreateClient().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Api_without_a_token_returns_401_problem_json()
    {
        var response = await api.CreateClient().GetAsync("/api/employees", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Hr_can_list_employees()
    {
        var (client, login) = await api.SignInAsHrAsync(Ct);

        var response = await client.GetAsync("/api/employees", Ct);

        Assert.Equal(("Hr", HttpStatusCode.OK), (login.Role, response.StatusCode));
    }

    [Fact]
    public async Task Employee_reads_only_their_own_payslip_and_no_hr_endpoints()
    {
        var (client, login) = await api.SignInAsync("ali@demo.local", ApiFixture.EmployeePassword, Ct);
        var ownId = login.EmployeeId!.Value;

        var own = await client.GetAsync($"/api/employees/{ownId}/payslip?month=2026-03", Ct);
        var other = await client.GetAsync($"/api/employees/{ownId + 1}/payslip?month=2026-03", Ct);
        var list = await client.GetAsync("/api/employees", Ct);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var payslip = await own.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(15950m, payslip.GetProperty("netPay").GetDecimal()); // the spec example, against the real database
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Employee_user_of_an_inactive_employee_cannot_log_in()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "youssef@demo.local", password = ApiFixture.EmployeePassword }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Invalid credentials.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_reuse_revokes_the_session()
    {
        var client = api.CreateClient(handleCookies: false);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFixture.HrEmail, password = ApiFixture.HrPassword }, Ct);
        var first = RefreshCookie(login);

        var rotated = await RefreshAsync(client, first);
        var second = RefreshCookie(rotated);
        var reuse = await RefreshAsync(client, first);
        var afterReuse = await RefreshAsync(client, second);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode); // the whole family was revoked
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var client = api.CreateClient(handleCookies: false);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFixture.HrEmail, password = ApiFixture.HrPassword }, Ct);
        var cookie = RefreshCookie(login);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"payroll_refresh={cookie}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, cookie)).StatusCode);
    }

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"payroll_refresh={cookie}");
        return await client.SendAsync(request, Ct);
    }

    private static string RefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("payroll_refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        return header["payroll_refresh=".Length..header.IndexOf(';', StringComparison.Ordinal)];
    }
}
