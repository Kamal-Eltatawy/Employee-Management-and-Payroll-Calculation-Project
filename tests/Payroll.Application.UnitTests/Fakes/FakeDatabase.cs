using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Departments;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;

namespace Payroll.Application.UnitTests.Fakes;

/// <summary>In-memory stand-in for the database: query filter, email reservation, row versions and "in use" checks behave like SQL Server.</summary>
public sealed class FakeDatabase
{
    public FakeDatabase()
    {
        Departments = new InMemoryDepartmentRepository();
        PayComponentTypes = new InMemoryPayComponentTypeRepository(this);
        Employees = new InMemoryEmployeeRepository(this);
        UnitOfWork = new FakeUnitOfWork(this);
    }

    public InMemoryDepartmentRepository Departments { get; }

    public InMemoryPayComponentTypeRepository PayComponentTypes { get; }

    public InMemoryEmployeeRepository Employees { get; }

    public FakeUnitOfWork UnitOfWork { get; }
}

public sealed class FakeUnitOfWork(FakeDatabase db) : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        db.Employees.Commit();
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDepartmentRepository : IDepartmentRepository
{
    private readonly List<Department> _departments = [];

    public Department Seed(int id, string name)
    {
        var department = new Department(id, name);
        _departments.Add(department);
        return department;
    }

    public Department? Find(int id) => _departments.Find(d => d.Id == id);

    public Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Department>>(_departments.OrderBy(d => d.Name).ToList());

    public Task<bool> ExistsAsync(int id, CancellationToken ct) => Task.FromResult(Find(id) is not null);
}

public sealed class InMemoryPayComponentTypeRepository(FakeDatabase db) : IPayComponentTypeRepository
{
    private readonly List<PayComponentType> _types = [];
    private int _nextId = 1;

    public PayComponentType Seed(string name, PayComponentKind kind, bool isTaxable = false)
    {
        var type = new PayComponentType(name, kind, isTaxable, DateTime.UtcNow);
        Add(type);
        return type;
    }

    public PayComponentType? Find(int id) => _types.Find(t => t.Id == id);

    public Task<IReadOnlyList<PayComponentType>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayComponentType>>(_types.ToList());

    public Task<PayComponentType?> GetByIdAsync(int id, CancellationToken ct) => Task.FromResult(Find(id));

    public Task<IReadOnlyList<PayComponentType>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayComponentType>>(_types.Where(t => ids.Contains(t.Id)).ToList());

    public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct) =>
        Task.FromResult(_types.Any(t =>
            string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) && t.Id != excludeId));

    public Task<bool> IsInUseAsync(int id, CancellationToken ct) =>
        Task.FromResult(db.Employees.AllIncludingDeleted.Any(e => e.PayComponents.Any(c => c.PayComponentTypeId == id)));

    public void Add(PayComponentType type)
    {
        PrivateSetter.Set(type, nameof(PayComponentType.Id), _nextId++);
        _types.Add(type);
    }

    public void Remove(PayComponentType type) => _types.Remove(type);
}

public sealed class InMemoryEmployeeRepository(FakeDatabase db) : IEmployeeRepository
{
    private readonly List<Employee> _employees = [];
    private int _nextId = 1;
    private readonly Dictionary<int, string> _committed = [];
    private long _rowVersion;

    public IReadOnlyList<Employee> AllIncludingDeleted => _employees;

    public Task<Employee?> GetByIdAsync(int id, CancellationToken ct) =>
        Task.FromResult(Active().FirstOrDefault(e => e.Id == id));

    public Task<PagedResult<Employee>> SearchAsync(EmployeeQuery query, CancellationToken ct)
    {
        var employees = Active();

        if (query.Search is { } search)
        {
            employees = employees.Where(e =>
                e.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (query.DepartmentId is { } departmentId)
        {
            employees = employees.Where(e => e.DepartmentId == departmentId);
        }

        if (query.Status is { } status)
        {
            employees = employees.Where(e => e.Status == status);
        }

        var filtered = employees.ToList();
        Func<Employee, object> key = query.SortBy switch
        {
            EmployeeSortBy.HireDate => e => e.HireDate,
            EmployeeSortBy.Salary => e => e.BaseMonthlySalary,
            _ => e => e.FullName.ToLowerInvariant(),
        };
        var sorted = query.SortDirection == SortDirection.Desc
            ? filtered.OrderByDescending(key).ThenBy(e => e.Id)
            : filtered.OrderBy(key).ThenBy(e => e.Id);

        var items = sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult(new PagedResult<Employee>(items, query.Page, query.PageSize, filtered.Count));
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeEmployeeId, CancellationToken ct) =>
        Task.FromResult(_employees.Any(e => e.Email == normalizedEmail && e.Id != excludeEmployeeId));

    public void Add(Employee employee)
    {
        PrivateSetter.Set(employee, nameof(Employee.Id), _nextId++);
        _employees.Add(employee);
    }

    /// <summary>Called on save: changed rows get a new row version, like SQL Server's rowversion.</summary>
    internal void Commit()
    {
        foreach (var employee in _employees)
        {
            var snapshot = Snapshot(employee);
            if (!_committed.TryGetValue(employee.Id, out var previous) || previous != snapshot)
            {
                PrivateSetter.Set(employee, nameof(Employee.RowVersion), BitConverter.GetBytes(++_rowVersion));
                _committed[employee.Id] = snapshot;
            }
        }
    }

    private static string Snapshot(Employee e) =>
        $"{e.FullName}|{e.Email}|{e.JobTitle}|{e.DepartmentId}|{e.HireDate}|{e.BaseMonthlySalary}|{e.Status}|{e.DeletedAtUtc}|" +
        string.Join(",", e.PayComponents.OrderBy(c => c.PayComponentTypeId).Select(c => $"{c.PayComponentTypeId}:{c.Amount}"));

    // Excludes soft-deleted rows (the EF query filter) and loads navigations (the EF Includes).
    private IEnumerable<Employee> Active() =>
        _employees.Where(e => e.DeletedAtUtc is null).Select(LoadNavigations);

    private Employee LoadNavigations(Employee employee)
    {
        PrivateSetter.Set(employee, nameof(Employee.Department), db.Departments.Find(employee.DepartmentId));
        foreach (var component in employee.PayComponents)
        {
            PrivateSetter.Set(component, nameof(EmployeePayComponent.PayComponentType),
                db.PayComponentTypes.Find(component.PayComponentTypeId));
        }

        return employee;
    }
}
