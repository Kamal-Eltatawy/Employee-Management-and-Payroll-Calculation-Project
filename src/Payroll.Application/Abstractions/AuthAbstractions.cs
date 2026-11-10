namespace Payroll.Application.Abstractions;

/// <summary>The caller, read from the JWT claims in the Api layer (S7).</summary>
public interface ICurrentUser
{
    bool IsInRole(string role);

    int? EmployeeId { get; }
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenGenerator
{
    AccessToken Generate(int userId, string email, string role, int? employeeId);
}

public sealed record UserCredentials(int UserId, string Email, string Role, int? EmployeeId);

/// <summary>Narrow port over ASP.NET Core Identity, so the login rule stays testable with fakes (S12).</summary>
public interface IUserCredentialStore
{
    /// <summary>Null when the email is unknown or the password is wrong.</summary>
    Task<UserCredentials?> ValidateAsync(string email, string password, CancellationToken ct);

    /// <summary>The user's current role and employee link (used on refresh, so changes apply at the next refresh).</summary>
    Task<UserCredentials?> FindByIdAsync(int userId, CancellationToken ct);
}

/// <summary>
/// A refresh token as stored (only its hash is persisted). Tokens issued from one login share a family (D55).
/// <c>RotatedAtUtc</c>: replaced by a newer token. <c>RevokedAtUtc</c>: the whole session was ended (logout, reuse, inactive).
/// </summary>
public sealed record StoredRefreshToken(
    Guid Id, int UserId, Guid FamilyId, DateTime ExpiresAtUtc, DateTime? RotatedAtUtc, DateTime? RevokedAtUtc);

/// <summary>The raw token is returned once, to be put in the HttpOnly cookie; it is never stored.</summary>
public sealed record IssuedRefreshToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Staging only: changes are committed by IUnitOfWork.SaveChangesAsync.</summary>
public interface IRefreshTokenStore
{
    IssuedRefreshToken Issue(int userId, Guid familyId, DateTime utcNow);

    Task<StoredRefreshToken?> FindAsync(string rawToken, CancellationToken ct);

    Task RotateAsync(Guid tokenId, DateTime utcNow, CancellationToken ct);

    Task RevokeFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken ct);
}
