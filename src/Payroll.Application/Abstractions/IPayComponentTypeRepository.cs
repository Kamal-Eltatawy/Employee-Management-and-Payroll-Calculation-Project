using Payroll.Domain.PayComponents;

namespace Payroll.Application.Abstractions;

public interface IPayComponentTypeRepository
{
    Task<IReadOnlyList<PayComponentType>> ListAsync(CancellationToken ct);

    Task<PayComponentType?> GetByIdAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<PayComponentType>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct);

    /// <summary>Counts deleted employees too (D7).</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken ct);

    /// <summary>Ids of the types assigned to at least one employee, deleted ones included (D7, D71).</summary>
    Task<IReadOnlySet<int>> GetUsedIdsAsync(CancellationToken ct);

    void Add(PayComponentType type);

    void Remove(PayComponentType type);
}
