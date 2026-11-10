using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Employees;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Api.IntegrationTests;

/// <summary>What only a real SQL Server can prove: the unique index, rowversion and the soft-delete filter (D47).</summary>
public sealed class PersistenceTests(ApiFixture api)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object NewEmployee(string email, string fullName = "Test Person") => new
    {
        fullName,
        email,
        jobTitle = "Tester",
        departmentId = 1,
        taxSchemeId = 1,
        hireDate = "2024-01-01",
        baseMonthlySalary = 10000m,
        status = "Active",
        payComponents = Array.Empty<object>(),
    };

    private static string UniqueEmail() => $"it-{Guid.NewGuid():N}@demo.local";

    private static async Task<JsonElement> CreateAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/employees", NewEmployee(email), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task Duplicate_email_through_the_api_is_409_on_the_email_field()
    {
        var (client, _) = await api.SignInAsHrAsync(Ct);
        var email = UniqueEmail();
        await CreateAsync(client, email);

        var duplicate = await client.PostAsJsonAsync("/api/employees", NewEmployee(email.ToUpperInvariant()), Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Unique_index_violation_is_translated_to_a_conflict_on_email()
    {
        var email = UniqueEmail();
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Employees.Add(new Employee("A", email, "Dev", 1, 1, new DateOnly(2024, 1, 1), 1000m, EmployeeStatus.Active, Today, DateTime.UtcNow));
        db.Employees.Add(new Employee("B", email, "Dev", 1, 1, new DateOnly(2024, 1, 1), 1000m, EmployeeStatus.Active, Today, DateTime.UtcNow));

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)db).SaveChangesAsync(Ct));

        Assert.True(conflict.Errors.ContainsKey("email"));
    }

    [Fact]
    public async Task Stale_row_version_through_the_api_is_409()
    {
        var (client, _) = await api.SignInAsHrAsync(Ct);
        var created = await CreateAsync(client, UniqueEmail());
        var id = created.GetProperty("id").GetInt32();
        var update = new Dictionary<string, object?>
        {
            ["fullName"] = "Renamed", ["email"] = created.GetProperty("email").GetString(), ["jobTitle"] = "Tester",
            ["departmentId"] = 1, ["taxSchemeId"] = 1, ["hireDate"] = "2024-01-01", ["baseMonthlySalary"] = 12000m,
            ["status"] = "Active", ["payComponents"] = Array.Empty<object>(),
            ["rowVersion"] = created.GetProperty("rowVersion").GetString(),
        };

        var first = await client.PutAsJsonAsync($"/api/employees/{id}", update, Ct);
        var stale = await client.PutAsJsonAsync($"/api/employees/{id}", update, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Concurrent_save_on_the_same_row_is_a_concurrency_conflict()
    {
        var (client, _) = await api.SignInAsHrAsync(Ct);
        var id = (await CreateAsync(client, UniqueEmail())).GetProperty("id").GetInt32();
        await using var first = api.Factory.Services.CreateAsyncScope();
        await using var second = api.Factory.Services.CreateAsyncScope();
        var firstDb = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await firstDb.Employees.SingleAsync(e => e.Id == id, Ct);
        var b = await secondDb.Employees.SingleAsync(e => e.Id == id, Ct);

        a.UpdateDetails("First", a.Email, a.JobTitle, 1, 1, a.HireDate, 11000m, a.Status, Today, DateTime.UtcNow);
        b.UpdateDetails("Second", b.Email, b.JobTitle, 1, 1, b.HireDate, 13000m, b.Status, Today, DateTime.UtcNow);
        await ((IUnitOfWork)firstDb).SaveChangesAsync(Ct);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)secondDb).SaveChangesAsync(Ct));
        Assert.Equal(ConflictMessages.Concurrency, conflict.Message);
    }

    [Fact]
    public async Task Soft_deleted_employee_is_hidden_but_kept_and_its_email_stays_reserved()
    {
        var (client, _) = await api.SignInAsHrAsync(Ct);
        var email = UniqueEmail();
        var id = (await CreateAsync(client, email)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/employees/{id}", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/employees/{id}", Ct)).StatusCode);
        var search = await client.GetFromJsonAsync<JsonElement>($"/api/employees?search={Uri.EscapeDataString(email)}", Ct);
        Assert.Equal(0, search.GetProperty("totalCount").GetInt32());
        var again = await client.PostAsJsonAsync("/api/employees", NewEmployee(email), Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        await using var scope = api.Factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Employees
            .IgnoreQueryFilters().SingleAsync(e => e.Id == id, Ct);
        Assert.NotNull(row.DeletedAtUtc);
    }
}
