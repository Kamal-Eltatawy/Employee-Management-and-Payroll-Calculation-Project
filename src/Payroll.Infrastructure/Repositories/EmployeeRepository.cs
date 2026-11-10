using Microsoft.EntityFrameworkCore;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Employees;
using Payroll.Domain.Payroll;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Repositories;

public sealed class EmployeeRepository(AppDbContext db) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Employees
            .Include(e => e.Department)
            .Include(e => e.TaxScheme)
            .Include(e => e.PayComponents).ThenInclude(c => c.PayComponentType)
            .AsSplitQuery()
            .SingleOrDefaultAsync(e => e.Id == id, ct);

    public async Task<PagedResult<Employee>> SearchAsync(EmployeeQuery query, CancellationToken ct)
    {
        var employees = db.Employees.AsNoTracking().Include(e => e.Department).AsQueryable();

        if (query.Search is { } search)
        {
            // Prefix search ("starts with") becomes LIKE 'x%', which can use the FullName and Email indexes (D62).
            // The default SQL Server collation is case-insensitive.
            employees = employees.Where(e => e.FullName.StartsWith(search) || e.Email.StartsWith(search));
        }

        if (query.DepartmentId is { } departmentId)
        {
            employees = employees.Where(e => e.DepartmentId == departmentId);
        }

        if (query.Status is { } status)
        {
            employees = employees.Where(e => e.Status == status);
        }

        var totalCount = await employees.CountAsync(ct);
        var items = await Sort(employees, query.SortBy, query.SortDirection)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Employee>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeEmployeeId, CancellationToken ct) =>
        db.Employees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Email == normalizedEmail && (excludeEmployeeId == null || e.Id != excludeEmployeeId), ct);

    public async Task<EmployeeHeadcount> GetHeadcountAsync(PayrollMonth month, CancellationToken ct)
    {
        var firstDay = month.FirstDay;
        var nextMonth = firstDay.AddMonths(1);
        var active = db.Employees.Where(e => e.Status == EmployeeStatus.Active);

        var total = await db.Employees.CountAsync(ct);
        var activeCount = await active.CountAsync(ct);
        var hired = await db.Employees.CountAsync(e => e.HireDate >= firstDay && e.HireDate < nextMonth, ct);
        var byDepartment = await active
            .GroupBy(e => new { e.DepartmentId, e.Department!.Name, e.Department.NameAr })
            .Select(g => new NamedCount(g.Key.Name, g.Key.NameAr, g.Count()))
            .ToListAsync(ct);
        var byTaxScheme = await active
            .GroupBy(e => new { e.TaxSchemeId, e.TaxScheme!.Name, e.TaxScheme.NameAr })
            .Select(g => new NamedCount(g.Key.Name, g.Key.NameAr, g.Count()))
            .ToListAsync(ct);

        return new EmployeeHeadcount(total, activeCount, hired, byDepartment, byTaxScheme);
    }

    public async Task<IReadOnlyList<Employee>> ListPayableBatchAsync(PayrollMonth month, int afterId, int batchSize, CancellationToken ct)
    {
        var nextMonth = month.FirstDay.AddMonths(1);
        return await db.Employees
            .AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active && e.HireDate < nextMonth && e.Id > afterId)
            .OrderBy(e => e.Id)
            .Take(batchSize)
            .Include(e => e.TaxScheme)
            .Include(e => e.PayComponents).ThenInclude(c => c.PayComponentType)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public void Add(Employee employee) => db.Employees.Add(employee);

    private static IOrderedQueryable<Employee> Sort(IQueryable<Employee> employees, EmployeeSortBy sortBy, SortDirection direction)
    {
        var descending = direction == SortDirection.Desc;
        var ordered = sortBy switch
        {
            EmployeeSortBy.HireDate => descending ? employees.OrderByDescending(e => e.HireDate) : employees.OrderBy(e => e.HireDate),
            EmployeeSortBy.Salary => descending
                ? employees.OrderByDescending(e => e.BaseMonthlySalary)
                : employees.OrderBy(e => e.BaseMonthlySalary),
            _ => descending ? employees.OrderByDescending(e => e.FullName) : employees.OrderBy(e => e.FullName),
        };

        // A unique tie-breaker keeps paging stable.
        return ordered.ThenBy(e => e.Id);
    }
}
