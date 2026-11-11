using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Payroll.Api.Auth;
using Payroll.Infrastructure.Identity;

namespace Payroll.Api.UnitTests.Auth;

public sealed class AuthTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "payroll-api",
        Audience = "payroll-client",
        SigningKey = "test-signing-key-that-is-at-least-32-characters",
        LifetimeMinutes = 60,
    };

    private static readonly DateTimeOffset Now = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Token_is_signed_and_carries_role_email_and_employee_id()
    {
        var generator = new JwtTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));

        var token = generator.Generate(3, "ali@demo.local", ["Employee"], 7);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = JwtTokenGenerator.CreateSigningKey(Options.SigningKey),
            ValidateLifetime = false,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("Employee", result.Claims["role"]);
        Assert.Equal("7", result.Claims["employee_id"]);
        Assert.Equal("ali@demo.local", result.Claims["email"]);
        Assert.Equal(Now.UtcDateTime.AddMinutes(60), token.ExpiresAtUtc);
    }

    [Fact]
    public void Hr_token_has_no_employee_id()
    {
        var generator = new JwtTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));

        var token = new JsonWebToken(generator.Generate(1, "hr@payroll.local", ["Hr"], null).Value);

        Assert.False(token.TryGetClaim("employee_id", out _));
    }

    [Fact]
    public async Task Several_roles_become_one_role_claim_each()
    {
        var generator = new JwtTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));

        var token = generator.Generate(4, "both@demo.local", ["Employee", "Hr"], 9);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = JwtTokenGenerator.CreateSigningKey(Options.SigningKey),
            ValidateLifetime = false,
            RoleClaimType = JwtTokenGenerator.RoleClaim,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.True(principal.IsInRole("Hr"));
        Assert.True(principal.IsInRole("Employee"));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void Signing_key_shorter_than_32_characters_is_rejected_at_startup(string key) =>
        Assert.Throws<InvalidOperationException>(() =>
            new JwtOptions { Issuer = "i", Audience = "a", SigningKey = key }.EnsureValid());

    [Fact]
    public void Current_user_reads_role_and_employee_id_from_claims()
    {
        var identity = new ClaimsIdentity([new Claim("role", "Employee"), new Claim("employee_id", "7")], "Bearer", "email", "role");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };

        var user = new CurrentUser(accessor);

        Assert.True(user.IsInRole("Employee"));
        Assert.False(user.IsInRole("Hr"));
        Assert.Equal(7, user.EmployeeId);
    }

    [Fact]
    public void Anonymous_caller_has_no_role_and_no_employee_id()
    {
        var user = new CurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.False(user.IsInRole("Hr"));
        Assert.Null(user.EmployeeId);
    }
}
