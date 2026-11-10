using Payroll.Domain.Common;

namespace Payroll.Domain.Departments;

/// <summary>Read-only master data (D8). Rows are seeded through the migrations.</summary>
public sealed class Department
{
    public const int NameMaxLength = 100;

    public Department(int id, string name)
    {
        Id = id;
        Name = Guard.RequiredText(name, NameMaxLength, nameof(Name));
    }

    public int Id { get; private set; }

    public string Name { get; private set; }
}
