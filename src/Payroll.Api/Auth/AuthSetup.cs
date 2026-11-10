using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Infrastructure.Identity;

namespace Payroll.Api.Auth;

public static class AuthPolicies
{
    public const string HrOnly = "HrOnly";
}

public static class AuthSetup
{
    /// <summary>JWT bearer validation + a fallback policy: everything requires a signed-in user unless marked anonymous (§12).</summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        jwt.EnsureValid();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the claim names exactly as issued ("role", "employee_id"); no legacy URI mapping.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtTokenGenerator.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RoleClaimType = JwtTokenGenerator.RoleClaim,
                    NameClaimType = JwtRegisteredClaimNames.Email,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthPolicies.HrOnly, policy => policy.RequireRole(Roles.Hr));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
