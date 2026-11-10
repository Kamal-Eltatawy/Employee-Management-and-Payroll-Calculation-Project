using FluentValidation;
using Mapster;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Employees;

namespace Payroll.Application.Employees;

public sealed class EmployeeService(
    IEmployeeRepository employees,
    IEmployeeReferenceChecker references,
    IUnitOfWork unitOfWork,
    IValidator<CreateEmployeeRequest> createValidator,
    IValidator<UpdateEmployeeRequest> updateValidator,
    IValidator<EmployeeSearchRequest> searchValidator,
    TimeProvider clock,
    TypeAdapterConfig mapping) : IEmployeeService
{
    public async Task<EmployeeResponse> GetAsync(int id, CancellationToken ct) =>
        (await LoadAsync(id, ct)).Adapt<EmployeeResponse>(mapping);

    public async Task<PagedResult<EmployeeListItem>> SearchAsync(EmployeeSearchRequest request, CancellationToken ct)
    {
        await searchValidator.ValidateAndThrowAsync(request, ct);

        var result = await employees.SearchAsync(EmployeeSearchOptions.ToQuery(request), ct);
        var items = result.Items.Select(employee => employee.Adapt<EmployeeListItem>(mapping)).ToList();
        return new PagedResult<EmployeeListItem>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await references.EnsureExistAsync(request, ct);
        await EnsureEmailAvailableAsync(request.Email, excludeEmployeeId: null, ct);

        var utcNow = clock.UtcNow();
        var employee = new Employee(
            request.FullName, request.Email, request.JobTitle, request.DepartmentId, request.TaxSchemeId,
            request.HireDate, request.BaseMonthlySalary, request.Status, clock.Today(), utcNow);
        employee.SetPayComponents(ToAssignments(request.PayComponents), utcNow);

        employees.Add(employee);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(employee.Id, ct);
    }

    public async Task<EmployeeResponse> UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var employee = await LoadAsync(id, ct);
        if (Convert.ToBase64String(employee.RowVersion) != request.RowVersion)
        {
            throw ConflictException.Coded(ErrorCodes.Concurrency, ConflictMessages.Concurrency);
        }

        await references.EnsureExistAsync(request, ct);
        await EnsureEmailAvailableAsync(request.Email, excludeEmployeeId: id, ct);

        var utcNow = clock.UtcNow();
        employee.UpdateDetails(
            request.FullName, request.Email, request.JobTitle, request.DepartmentId, request.TaxSchemeId,
            request.HireDate, request.BaseMonthlySalary, request.Status, clock.Today(), utcNow);
        employee.SetPayComponents(ToAssignments(request.PayComponents), utcNow);

        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var employee = await LoadAsync(id, ct);
        employee.MarkDeleted(clock.UtcNow());
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Employee> LoadAsync(int id, CancellationToken ct) =>
        await employees.GetByIdAsync(id, ct) ?? throw new NotFoundException("Employee", id);

    private async Task EnsureEmailAvailableAsync(string email, int? excludeEmployeeId, CancellationToken ct)
    {
        if (await employees.EmailExistsAsync(Employee.NormalizeEmail(email), excludeEmployeeId, ct))
        {
            throw ConflictException.Coded(ErrorCodes.EmailInUse, ConflictMessages.EmailInUse, "email");
        }
    }

    private static List<PayComponentAssignment> ToAssignments(IReadOnlyList<PayComponentAssignmentDto> components) =>
        components.Select(c => new PayComponentAssignment(c.PayComponentTypeId, c.Amount)).ToList();
}
