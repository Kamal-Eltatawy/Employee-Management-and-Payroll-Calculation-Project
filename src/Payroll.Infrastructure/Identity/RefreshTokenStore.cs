using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payroll.Application.Abstractions;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure.Identity;

public sealed class RefreshTokenStore(AppDbContext db, IOptions<JwtOptions> options) : IRefreshTokenStore
{
    public IssuedRefreshToken Issue(int userId, Guid familyId, DateTime utcNow)
    {
        // 256 bits of randomness; URL-safe so it can live in a cookie as-is.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expires = utcNow.AddDays(options.Value.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = expires,
        });

        return new IssuedRefreshToken(raw, expires);
    }

    public async Task<StoredRefreshToken?> FindAsync(string rawToken, CancellationToken ct)
    {
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        return token is null
            ? null
            : new StoredRefreshToken(token.Id, token.UserId, token.FamilyId, token.ExpiresAtUtc, token.RotatedAtUtc, token.RevokedAtUtc);
    }

    public async Task RotateAsync(Guid tokenId, DateTime utcNow, CancellationToken ct)
    {
        var token = await db.RefreshTokens.SingleAsync(t => t.Id == tokenId, ct);
        token.RotatedAtUtc ??= utcNow;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAtUtc = utcNow;
        }
    }

    public static string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
