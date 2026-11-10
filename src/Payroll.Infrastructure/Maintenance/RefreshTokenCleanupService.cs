using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Maintenance;

/// <summary>
/// Deletes expired refresh tokens every few hours so the table does not grow forever (D62).
/// Expired tokens can no longer be used or reused, so nothing depends on them.
/// </summary>
public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = clock.GetUtcNow().UtcDateTime;
                var deleted = await db.RefreshTokens.Where(t => t.ExpiresAtUtc < now).ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} expired refresh tokens", deleted);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A background job must not stop the host; the next run tries again.
                logger.LogWarning(exception, "Refresh token cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
