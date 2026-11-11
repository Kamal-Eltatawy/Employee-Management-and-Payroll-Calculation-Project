using Microsoft.AspNetCore.Identity;
using Payroll.Application.Abstractions;

namespace Payroll.Infrastructure.Identity;

/// <summary>Wraps UserManager behind the narrow IUserCredentialStore port (S12).</summary>
public sealed class IdentityCredentialStore(UserManager<ApplicationUser> users) : IUserCredentialStore
{
    // Same hashing cost as a real check, so an unknown email answers as slowly as a wrong password (D25).
    private static readonly ApplicationUser NoUser = new();
    private static readonly string DummyHash = new PasswordHasher<ApplicationUser>().HashPassword(NoUser, Guid.NewGuid().ToString());

    public async Task<UserCredentials?> ValidateAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            users.PasswordHasher.VerifyHashedPassword(NoUser, DummyHash, password);
            return null;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            // Counts towards the lockout (D66): MaxFailedAccessAttempts failures lock the account for a while.
            await users.AccessFailedAsync(user);
            return null;
        }

        if (await users.GetAccessFailedCountAsync(user) > 0)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        return await ToCredentialsAsync(user);
    }

    public async Task<UserCredentials?> FindByIdAsync(int userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return user is null ? null : await ToCredentialsAsync(user);
    }

    /// <summary>All roles of the user (D67). A user without any role cannot sign in.</summary>
    private async Task<UserCredentials?> ToCredentialsAsync(ApplicationUser user)
    {
        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
        return roles.Count == 0 || user.Email is null ? null : new UserCredentials(user.Id, user.Email, roles, user.EmployeeId);
    }
}
