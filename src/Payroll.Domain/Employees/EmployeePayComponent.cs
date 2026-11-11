using Payroll.Domain.Common;
using Payroll.Domain.PayComponents;

namespace Payroll.Domain.Employees;

/// <summary>A pay component type assigned to an employee with an amount. Part of the Employee aggregate.</summary>
public sealed class EmployeePayComponent
{
    internal EmployeePayComponent(int payComponentTypeId, decimal amount)
    {
        PayComponentTypeId = Guard.PositiveId(payComponentTypeId, nameof(PayComponentTypeId));
        Amount = Guard.Positive(amount, nameof(Amount));
    }

    private EmployeePayComponent()
    {
    }

    public int Id { get; private set; }

    public int EmployeeId { get; private set; }

    public int PayComponentTypeId { get; private set; }

    public PayComponentType? PayComponentType { get; private set; }

    public decimal Amount { get; private set; }

    internal void ChangeAmount(decimal amount) => Amount = Guard.Positive(amount, nameof(Amount));
}
