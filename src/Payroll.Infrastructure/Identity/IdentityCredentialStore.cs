using Microsoft.AspNetCore.Identity;
using Payroll.Application.Abstractions;

namespace Payroll.Infrastructure.Identity;

/// <summary>Wraps UserManager behind the narrow IUserCredentialStore port (S12).</summary>
public sealed class IdentityCredentialStore(UserManager<ApplicationUser> users) : IUserCredentialStore
{
    public async Task<UserCredentials?> ValidateAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return await ToCredentialsAsync(user);
    }

    public async Task<UserCredentials?> FindByIdAsync(int userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return user is null ? null : await ToCredentialsAsync(user);
    }

    private async Task<UserCredentials?> ToCredentialsAsync(ApplicationUser user)
    {
        var role = (await users.GetRolesAsync(user)).FirstOrDefault();
        return role is null || user.Email is null ? null : new UserCredentials(user.Id, user.Email, role, user.EmployeeId);
    }
}
