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
        var employees = new EmployeeService(_db.Employees, _db.Departments, _db.PayComponentTypes, _db.TaxSchemes, _db.UnitOfWork,
            new CreateEmployeeRequestValidator(clock), new UpdateEmployeeRequestValidator(clock), new EmployeeSearchRequestValidator(),
            clock, MappingConfig.Create());
        var employee = await employees.CreateAsync(TestData.CreateRequest(components: [new(type.Id, 500m)]), Ct);
        await employees.DeleteAsync(employee.Id, Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(type.Id, Ct));
    }

    [Fact]
    public async Task Unknown_type_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(99, new UpdatePayComponentTypeRequest("X", false), Ct));
}
