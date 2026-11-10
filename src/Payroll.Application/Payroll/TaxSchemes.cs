using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Payroll;

public sealed record TaxSchemeResponse(int Id, string Name, TaxMethod Method);

public interface ITaxSchemeService
{
    Task<IReadOnlyList<TaxSchemeResponse>> ListAsync(CancellationToken ct);
}

public sealed class TaxSchemeService(ITaxSchemeRepository schemes) : ITaxSchemeService
{
    public async Task<IReadOnlyList<TaxSchemeResponse>> ListAsync(CancellationToken ct) =>
        (await schemes.ListAsync(ct)).Select(s => new TaxSchemeResponse(s.Id, LocalizedName.Pick(s.Name, s.NameAr), s.Method)).ToList();
}
