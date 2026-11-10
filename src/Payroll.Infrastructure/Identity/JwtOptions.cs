namespace Payroll.Infrastructure.Identity;

/// <summary>Bound from the "Jwt" section (Jwt__Issuer, Jwt__Audience, Jwt__SigningKey in .env).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Short-lived access token; the refresh token renews it (D55).</summary>
    public int LifetimeMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Jwt__Issuer and Jwt__Audience must be set (see .env.example).");
        }

        if (SigningKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException($"Jwt__SigningKey must be at least {MinimumSigningKeyLength} characters (see .env.example).");
        }
    }
}
