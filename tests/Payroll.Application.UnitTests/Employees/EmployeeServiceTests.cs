using FluentValidation;
using Payroll.Application.Common;
using Payroll.Application.Employees;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Application.UnitTests.Employees;

public sealed class EmployeeServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly EmployeeService _service;
    private readonly int _transportId;
    private readonly int _loanId;

    public EmployeeServiceTests()
    {
        _db.Departments.Seed(1, "Engineering");
        _db.Departments.Seed(2, "Finance");
        _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        _db.TaxSchemes.Seed(2, "Flat Rate", TaxMethod.Flat);
        _transportId = _db.PayComponentTypes.Seed("Transport", PayComponentKind.Allowance).Id;
        _loanId = _db.PayComponentTypes.Seed("Loan Repayment", PayComponentKind.Deduction).Id;

        var clock = TestData.Clock();
        _service = new EmployeeService(
            _db.Employees, _db.Departments, _db.PayComponentTypes, _db.TaxSchemes, _db.UnitOfWork,
            new CreateEmployeeRequestValidator(clock), new UpdateEmployeeRequestValidator(clock),
            new EmployeeSearchRequestValidator(), clock, MappingConfig.Create());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<EmployeeResponse> CreateAsync(string email = "ali@demo.local", string name = "Ali Hassan") =>
        _service.CreateAsync(TestData.CreateRequest(fullName: name, email: email,
            components: [new(_transportId, 1000m), new(_loanId, 750m)]), Ct);

    [Fact]
    public async Task Create_returns_the_saved_employee_with_department_components_and_row_version()
    {
        var created = await _service.CreateAsync(
            TestData.CreateRequest(email: "  Ali@Demo.Local ", components: [new(_transportId, 1000m)]), Ct);

        Assert.Equal("ali@demo.local", created.Email);
        Assert.Equal("Engineering", created.DepartmentName);
        Assert.Equal((1, "Standard"), (created.TaxSchemeId, created.TaxSchemeName));
        var component = Assert.Single(created.PayComponents);
        Assert.Equal(("Transport", PayComponentKind.Allowance, 1000m), (component.Name, component.Kind, component.Amount));
        Assert.NotEmpty(created.RowVersion);
        Assert.Equal(1, _db.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_with_an_email_in_use_throws_conflict_on_the_email_field()
    {
        await CreateAsync();

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(email: "ALI@demo.local"));

        Assert.True(conflict.Errors.ContainsKey("email"));
    }

    [Fact]
    public async Task Create_with_the_email_of_a_deleted_employee_throws_conflict()
    {
        var deleted = await CreateAsync();
        await _service.DeleteAsync(deleted.Id, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync());
    }

    [Fact]
    public async Task Create_with_unknown_department_and_type_throws_validation_errors_for_those_fields()
    {
        var request = TestData.CreateRequest(departmentId: 99, components: [new(_transportId, 10m), new(42, 10m)]);

        var error = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, Ct));

        Assert.Equal(new[] { "DepartmentId", "PayComponents[1].PayComponentTypeId" },
            error.Errors.Select(e => e.PropertyName).ToArray());
        Assert.Empty(_db.Employees.AllIncludingDeleted);
    }

    [Fact]
    public async Task Create_with_invalid_input_throws_validation_and_saves_nothing()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(TestData.CreateRequest(fullName: ""), Ct));

        Assert.Equal(0, _db.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Get_unknown_employee_throws_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(404, Ct));

    [Fact]
    public async Task Deleted_employee_disappears_from_get_and_search()
    {
        var kept = await CreateAsync("kept@demo.local", "Kept");
        var deleted = await CreateAsync("gone@demo.local", "Gone");

        await _service.DeleteAsync(deleted.Id, Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(deleted.Id, Ct));
        var page = await _service.SearchAsync(new EmployeeSearchRequest(null, null, null, null, null), Ct);
        Assert.Equal(kept.Id, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
        Assert.NotNull(_db.Employees.AllIncludingDeleted.Single(e => e.Id == deleted.Id).DeletedAtUtc);
    }

    [Fact]
    public async Task Delete_unknown_employee_throws_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(404, Ct));

    [Fact]
    public async Task Update_of_a_deleted_employee_throws_not_found()
    {
        var created = await CreateAsync();
        await _service.DeleteAsync(created.Id, Ct);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(created.Id, TestData.UpdateRequest(created), Ct));
    }

    [Fact]
    public async Task Update_with_a_stale_row_version_throws_conflict()
    {
        var created = await CreateAsync();
        await _service.UpdateAsync(created.Id, TestData.UpdateRequest(created), Ct);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(created.Id, TestData.UpdateRequest(created), Ct));

        Assert.Equal(ConflictMessages.Concurrency, conflict.Message);
    }

    [Fact]
    public async Task Update_changes_details_replaces_components_and_returns_a_new_row_version()
    {
        var created = await CreateAsync();
        var request = TestData.UpdateRequest(created, components: [new(_loanId, 500m)]) with
        {
            FullName = "Ali H.", DepartmentId = 2, TaxSchemeId = 2,
        };

        var updated = await _service.UpdateAsync(created.Id, request, Ct);

        Assert.Equal(("Ali H.", "Finance", "Flat Rate"), (updated.FullName, updated.DepartmentName, updated.TaxSchemeName));
        Assert.Equal((_loanId, 500m), (Assert.Single(updated.PayComponents).PayComponentTypeId, updated.PayComponents[0].Amount));
        Assert.NotEqual(created.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task Update_keeping_its_own_email_is_allowed_but_taking_another_is_a_conflict()
    {
        var ali = await CreateAsync();
        await CreateAsync("sara@demo.local", "Sara");

        var same = await _service.UpdateAsync(ali.Id, TestData.UpdateRequest(ali), Ct);
        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(ali.Id, TestData.UpdateRequest(same, email: "sara@demo.local"), Ct));

        Assert.True(conflict.Errors.ContainsKey("email"));
    }

    [Fact]
    public async Task Search_filters_sorts_and_pages()
    {
        await CreateAsync("c@demo.local", "Charlie");
        await CreateAsync("a@demo.local", "alice");
        await CreateAsync("b@demo.local", "Bob");

        var page = await _service.SearchAsync(
            new EmployeeSearchRequest(null, 1, EmployeeStatus.Active, "name", "desc", Page: 1, PageSize: 2), Ct);

        Assert.Equal(new[] { "Charlie", "Bob" }, page.Items.Select(i => i.FullName).ToArray());
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("Engineering", page.Items[0].DepartmentName);
    }

    [Fact]
    public async Task Search_matches_name_or_email_case_insensitively()
    {
        await CreateAsync("ali@demo.local", "Ali Hassan");
        await CreateAsync("sara@demo.local", "Sara Ahmed");

        var byName = await _service.SearchAsync(new EmployeeSearchRequest("HASS", null, null, null, null), Ct);
        var byEmail = await _service.SearchAsync(new EmployeeSearchRequest(" SARA@ ", null, null, null, null), Ct);

        Assert.Equal("Ali Hassan", Assert.Single(byName.Items).FullName);
        Assert.Equal("Sara Ahmed", Assert.Single(byEmail.Items).FullName);
    }

    [Fact]
    public async Task Search_with_invalid_sort_throws_validation() =>
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SearchAsync(new EmployeeSearchRequest(null, null, null, "email", null), Ct));

    [Fact]
    public async Task Create_with_unknown_tax_scheme_throws_validation_error_for_that_field()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(TestData.CreateRequest(taxSchemeId: 9), Ct));

        Assert.Equal("TaxSchemeId", Assert.Single(error.Errors).PropertyName);
    }
}
