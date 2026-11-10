using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(Payroll.Api.IntegrationTests.ApiFixture))]

namespace Payroll.Api.IntegrationTests;

/// <summary>
/// One real SQL Server container and one API host for the whole assembly (D47).
/// The API starts in Development, so it applies the migrations and seeds the demo data exactly as in local runs.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string HrEmail = "hr@payroll.local";
    public const string HrPassword = "Integration_Hr1!";
    public const string EmployeePassword = "Integration_Emp1!";

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync(TestContext.Current.CancellationToken);

        // Set before the host starts; the .env loader never overrides variables that are already set.
        var connectionString = new SqlConnectionStringBuilder(_database.GetConnectionString())
        {
            InitialCatalog = "PayrollIntegrationTests",
        }.ConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "payroll-api-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "payroll-client-tests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-tests-signing-key-at-least-32-characters");
        Environment.SetEnvironmentVariable("Seed__HrPassword", HrPassword);
        Environment.SetEnvironmentVariable("Seed__EmployeePassword", EmployeePassword);
        Environment.SetEnvironmentVariable("Auth__RefreshReuseGraceSeconds", "0"); // so the reuse test sees reuse immediately
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "1000"); // many logins from one test client

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        _ = Factory.Server; // starts the host: migrations + seed
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>HTTPS base address so the Secure refresh cookie is kept and sent by the cookie container.</summary>
    public HttpClient CreateClient(bool handleCookies = true) =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });

    public async Task<(HttpClient Client, LoginBody Login)> SignInAsync(string email, string password, CancellationToken ct)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<LoginBody>(ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return (client, login);
    }

    public Task<(HttpClient Client, LoginBody Login)> SignInAsHrAsync(CancellationToken ct) => SignInAsync(HrEmail, HrPassword, ct);
}

public sealed record LoginBody(string AccessToken, DateTime ExpiresAtUtc, string Role, int? EmployeeId, string Email);
