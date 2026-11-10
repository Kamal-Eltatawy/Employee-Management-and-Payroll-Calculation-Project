namespace Payroll.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>Commits the changes. Concurrency and unique-index failures surface as <c>ConflictException</c> (S6).</summary>
    Task SaveChangesAsync(CancellationToken ct);
}
