using Payroll.Application.Abstractions;
using Payroll.Application.Common;

namespace Payroll.Application.UnitTests.Fakes;

public sealed class FakeCurrentUser(string role = Roles.Hr, int? employeeId = null) : ICurrentUser
{
    public static FakeCurrentUser Hr() => new();

    public static FakeCurrentUser Employee(int employeeId) => new(Roles.Employee, employeeId);

    public int? EmployeeId { get; } = employeeId;

    public bool IsInRole(string requested) => requested == role;
}

public sealed class FakeCredentialStore : IUserCredentialStore
{
    private readonly Dictionary<string, (string Password, UserCredentials User)> _users = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string email, string password, string role, int? employeeId = null) =>
        _users[email] = (password, new UserCredentials(_users.Count + 1, email, role, employeeId));

    public Task<UserCredentials?> ValidateAsync(string email, string password, CancellationToken ct) =>
        Task.FromResult(_users.TryGetValue(email, out var entry) && entry.Password == password ? entry.User : null);

    public Task<UserCredentials?> FindByIdAsync(int userId, CancellationToken ct) =>
        Task.FromResult(_users.Values.Select(v => v.User).FirstOrDefault(u => u.UserId == userId));

    public void Remove(string email) => _users.Remove(email);
}

/// <summary>In-memory refresh tokens, keyed by the raw value (the real store keys by hash).</summary>
public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private readonly Dictionary<string, StoredRefreshToken> _tokens = [];
    private int _next = 1;

    public IReadOnlyCollection<StoredRefreshToken> All => _tokens.Values;

    public IssuedRefreshToken Issue(int userId, Guid familyId, DateTime utcNow)
    {
        var raw = $"refresh-{_next++}";
        var expires = utcNow.AddDays(7);
        _tokens[raw] = new StoredRefreshToken(Guid.NewGuid(), userId, familyId, expires, null, null);
        return new IssuedRefreshToken(raw, expires);
    }

    public Task<StoredRefreshToken?> FindAsync(string rawToken, CancellationToken ct) =>
        Task.FromResult(_tokens.GetValueOrDefault(rawToken));

    /// <summary>Usable: neither rotated nor revoked.</summary>
    public IEnumerable<StoredRefreshToken> Active => _tokens.Values.Where(t => t.RotatedAtUtc is null && t.RevokedAtUtc is null);

    public Task RotateAsync(Guid tokenId, DateTime utcNow, CancellationToken ct)
    {
        foreach (var key in _tokens.Where(pair => pair.Value.Id == tokenId).Select(pair => pair.Key).ToList())
        {
            _tokens[key] = _tokens[key] with { RotatedAtUtc = _tokens[key].RotatedAtUtc ?? utcNow };
        }

        return Task.CompletedTask;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken ct)
    {
        foreach (var key in _tokens.Where(pair => pair.Value.FamilyId == familyId && pair.Value.RevokedAtUtc is null)
                     .Select(pair => pair.Key).ToList())
        {
            _tokens[key] = _tokens[key] with { RevokedAtUtc = utcNow };
        }

        return Task.CompletedTask;
    }
}

/// <summary>Records what was put in the token, so tests can check the claims without parsing a JWT.</summary>
public sealed class FakeTokenGenerator : ITokenGenerator
{
    public (int UserId, string Email, string Role, int? EmployeeId)? LastIssued { get; private set; }

    public AccessToken Generate(int userId, string email, string role, int? employeeId)
    {
        LastIssued = (userId, email, role, employeeId);
        return new AccessToken($"token-for-{email}", new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc));
    }
}
