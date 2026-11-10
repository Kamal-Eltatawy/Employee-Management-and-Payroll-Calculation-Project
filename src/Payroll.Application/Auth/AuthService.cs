using FluentValidation;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Employees;

namespace Payroll.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>The JSON body. The refresh token is not here: it travels only in an HttpOnly cookie (D55).</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string Role, int? EmployeeId);

/// <summary>What the controller needs: the body plus the refresh token to put in the cookie.</summary>
public sealed record AuthResult(LoginResponse Response, IssuedRefreshToken RefreshToken);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Rotates the refresh token and issues a new access token (D55).</summary>
    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct);

    /// <summary>Revokes every token of the session. Always succeeds.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken ct);
}

/// <summary>
/// D25: a wrong password, an unknown email, and an Employee user whose employee is inactive or deleted
/// all get the same 401. D55: refresh tokens rotate on every use; presenting a revoked one again
/// (a sign of theft) revokes the whole family, which ends the session everywhere.
/// </summary>
public sealed class AuthService(
    IUserCredentialStore credentials,
    IEmployeeRepository employees,
    ITokenGenerator tokens,
    IRefreshTokenStore refreshTokens,
    IUnitOfWork unitOfWork,
    IValidator<LoginRequest> validator,
    TimeProvider clock) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var user = await credentials.ValidateAsync(request.Email.Trim(), request.Password, ct)
            ?? throw new UnauthorizedException();
        await EnsureMayHaveSessionAsync(user, ct);

        return await IssueAsync(user, familyId: Guid.NewGuid(), ct);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        var utcNow = clock.UtcNow();
        var stored = string.IsNullOrWhiteSpace(refreshToken) ? null : await refreshTokens.FindAsync(refreshToken, ct);
        if (stored is null)
        {
            throw new UnauthorizedException();
        }

        if (stored.RevokedAtUtc is not null)
        {
            // Reuse of a rotated token: someone else may hold the family. End it.
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, utcNow, ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException();
        }

        var user = stored.ExpiresAtUtc > utcNow ? await credentials.FindByIdAsync(stored.UserId, ct) : null;
        if (user is null || !await MayHaveSessionAsync(user, ct))
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, utcNow, ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException();
        }

        await refreshTokens.RevokeAsync(stored.Id, utcNow, ct);
        return await IssueAsync(user, stored.FamilyId, ct);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        var stored = string.IsNullOrWhiteSpace(refreshToken) ? null : await refreshTokens.FindAsync(refreshToken, ct);
        if (stored is not null)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, clock.UtcNow(), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResult> IssueAsync(UserCredentials user, Guid familyId, CancellationToken ct)
    {
        var refresh = refreshTokens.Issue(user.UserId, familyId, clock.UtcNow());
        await unitOfWork.SaveChangesAsync(ct);

        var access = tokens.Generate(user.UserId, user.Email, user.Role, user.EmployeeId);
        return new AuthResult(new LoginResponse(access.Value, access.ExpiresAtUtc, user.Role, user.EmployeeId), refresh);
    }

    private async Task EnsureMayHaveSessionAsync(UserCredentials user, CancellationToken ct)
    {
        if (!await MayHaveSessionAsync(user, ct))
        {
            throw new UnauthorizedException();
        }
    }

    /// <summary>Hr always; an Employee only while the linked employee exists and is Active (D25).</summary>
    private async Task<bool> MayHaveSessionAsync(UserCredentials user, CancellationToken ct)
    {
        if (user.Role == Roles.Hr)
        {
            return true;
        }

        if (user.Role != Roles.Employee || user.EmployeeId is not { } employeeId)
        {
            return false;
        }

        return await employees.GetByIdAsync(employeeId, ct) is { Status: EmployeeStatus.Active };
    }
}
