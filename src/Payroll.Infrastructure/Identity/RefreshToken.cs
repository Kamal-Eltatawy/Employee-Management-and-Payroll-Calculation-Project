namespace Payroll.Infrastructure.Identity;

/// <summary>Persisted refresh token (D55). Only the SHA-256 hash of the token is stored.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public int UserId { get; set; }

    /// <summary>All tokens rotated from one login share a family; reuse revokes the family.</summary>
    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
