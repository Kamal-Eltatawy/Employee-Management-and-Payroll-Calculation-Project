using FluentValidation;
using Payroll.Application.Common;
using Payroll.Application.Employees;
using Payroll.Application.PayComponents;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Application.UnitTests.PayComponents;

public sealed class PayComponentTypeServiceTests
{
    private readonly FakeDatabase _db = new();
    private readonly PayComponentTypeService _service;

    public PayComponentTypeServiceTests() =>
        _service = new PayComponentTypeService(_db.PayComponentTypes, _db.UnitOfWork,
            new CreatePayComponentTypeRequestValidator(), new UpdatePayComponentTypeRequestValidator(),
            TestData.Clock(), MappingConfig.Create());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_returns_the_new_type()
    {
        var created = await _service.CreateAsync(new CreatePayComponentTypeRequest(" Phone ", PayComponentKind.Allowance, true), Ct);

        Assert.Equal(("Phone", PayComponentKind.Allowance, true), (created.Name, created.Kind, created.IsTaxable));
        Assert.Equal(created, await _service.GetAsync(created.Id, Ct));
    }

    [Fact]
    public async Task Taxable_deduction_is_rejected_on_create_and_on_update()
    {
        var create = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new CreatePayComponentTypeRequest("Union Fee", PayComponentKind.Deduction, true), Ct));
        var deduction = await _service.CreateAsync(new CreatePayComponentTypeRequest("Union Fee", PayComponentKind.Deduction, false), Ct);
        var update = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(deduction.Id, new UpdatePayComponentTypeRequest("Union Fee", true), Ct));

        Assert.Equal("IsTaxable", Assert.Single(create.Errors).PropertyName);
        Assert.Equal("IsTaxable", Assert.Single(update.Errors).PropertyName);
    }

    [Fact]
    public async Task Duplicate_name_is_a_conflict_on_the_name_field()
    {
        await _service.CreateAsync(new CreatePayComponentTypeRequest("Transport", PayComponentKind.Allowance, false), Ct);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(new CreatePayComponentTypeRequest("transport", PayComponentKind.Deduction, false), Ct));

        Assert.True(conflict.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task Update_renames_and_changes_taxability()
    {
        var type = await _service.CreateAsync(new CreatePayComponentTypeRequest("Meal", PayComponentKind.Allowance, false), Ct);

        var updated = await _service.UpdateAsync(type.Id, new UpdatePayComponentTypeRequest("Meals", true), Ct);

        Assert.Equal(("Meals", PayComponentKind.Allowance, true), (updated.Name, updated.Kind, updated.IsTaxable));
    }

    [Fact]
    public async Task Unused_type_can_be_deleted()
    {
        var type = await _service.CreateAsync(new CreatePayComponentTypeRequest("Bonus", PayComponentKind.Allowance, true), Ct);

        await _service.DeleteAsync(type.Id, Ct);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(type.Id, Ct));
    }

    [Fact]
    public async Task Type_used_by_a_deleted_employee_cannot_be_deleted()
    {
        var type = await _service.CreateAsync(new CreatePayComponentTypeRequest("Transport", PayComponentKind.Allowance, false), Ct);
        _db.Departments.Seed(1, "Engineering");
        _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        var clock = TestData.Clock();
        var employees = TestData.EmployeeService(_db, clock);
        var employee = await employees.CreateAsync(TestData.CreateRequest(components: [new(type.Id, 500m)]), Ct);
        await employees.DeleteAsync(employee.Id, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(type.Id, Ct));
    }

    private async Task<int> UseInDeletedEmployeeAsync(int typeId)
    {
        _db.Departments.Seed(1, "Engineering");
        _db.TaxSchemes.Seed(1, "Standard", TaxMethod.Progressive);
        var employees = TestData.EmployeeService(_db, TestData.Clock());
        var employee = await employees.CreateAsync(TestData.CreateRequest(components: [new(typeId, 500m)]), Ct);
        await employees.DeleteAsync(employee.Id, Ct);
        return employee.Id;
    }

    [Fact]
    public async Task Kind_can_change_while_the_type_is_unused()
    {
        var type = await _service.CreateAsync(new CreatePayComponentTypeRequest("Uniform", PayComponentKind.Allowance, true), Ct);

        var updated = await _service.UpdateAsync(type.Id,
            new UpdatePayComponentTypeRequest("Uniform", false, Kind: PayComponentKind.Deduction), Ct);

        Assert.Equal((PayComponentKind.Deduction, false, false), (updated.Kind, updated.IsTaxable, updated.IsInUse));
    }

    [Fact]
    public async Task Kind_cannot_change_once_the_type_is_used_even_by_a_deleted_employee()
    {
        var type = await _service.CreateAsync(new CreatePayComponentTypeRequest("Transport", PayComponentKind.Allowance, false), Ct);
        await UseInDeletedEmployeeAsync(type.Id);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(type.Id,
            new UpdatePayComponentTypeRequest("Transport", false, Kind: PayComponentKind.Deduction), Ct));

        Assert.Equal(ErrorCodes.PayComponentTypeKindInUse, conflict.Code);
        Assert.True(conflict.Errors.ContainsKey("kind"));
        Assert.Equal(PayComponentKind.Allowance, (await _service.GetAsync(type.Id, Ct)).Kind);

        // The same kind, or no kind, is still a normal update.
        var renamed = await _service.UpdateAsync(type.Id,
            new UpdatePayComponentTypeRequest("Transport allowance", false, Kind: PayComponentKind.Allowance), Ct);
        Assert.Equal(("Transport allowance", true), (renamed.Name, renamed.IsInUse));
    }

    [Fact]
    public async Task List_and_get_report_whether_a_type_is_in_use()
    {
        var used = await _service.CreateAsync(new CreatePayComponentTypeRequest("Housing", PayComponentKind.Allowance, true), Ct);
        var unused = await _service.CreateAsync(new CreatePayComponentTypeRequest("Bonus", PayComponentKind.Allowance, true), Ct);
        await UseInDeletedEmployeeAsync(used.Id);

        var list = await _service.ListAsync(Ct);

        Assert.True(list.Single(t => t.Id == used.Id).IsInUse);
        Assert.False(list.Single(t => t.Id == unused.Id).IsInUse);
        Assert.True((await _service.GetAsync(used.Id, Ct)).IsInUse);
    }

    [Fact]
    public async Task Changing_to_a_deduction_must_not_keep_it_taxable()
    {
        var type = await _service.CreateAsync(new CreatePayComponentTypeRequest("Gym", PayComponentKind.Allowance, true), Ct);

        var error = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateAsync(type.Id,
            new UpdatePayComponentTypeRequest("Gym", true, Kind: PayComponentKind.Deduction), Ct));

        Assert.Equal("IsTaxable", Assert.Single(error.Errors).PropertyName);
    }

    [Fact]
    public async Task Unknown_type_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(99, new UpdatePayComponentTypeRequest("X", false), Ct));
}
