using Payroll.Domain.Payroll;

namespace Payroll.Application.Abstractions;

public interface ITaxSchemeRepository
{
    Task<IReadOnlyList<TaxScheme>> ListAsync(CancellationToken ct);

    Task<TaxScheme?> GetByIdAsync(int id, CancellationToken ct);

    Task<bool> ExistsAsync(int id, CancellationToken ct);
}
