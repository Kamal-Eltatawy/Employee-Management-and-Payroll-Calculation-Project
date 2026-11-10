using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Payroll.Api.Localization;

namespace Payroll.Api.Auth;

/// <summary>Bound from "RateLimiting" (e.g. RateLimiting__AuthPermitLimit). Tests raise the limit.</summary>
public sealed class AuthRateLimitOptions
{
    public int AuthPermitLimit { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Login and refresh are limited per client IP (fixed window) against password guessing and token abuse (D62).
/// Built into ASP.NET Core: no extra package. A rejected call gets 429 problem+json.
/// </summary>
public static class AuthRateLimiting
{
    public const string Policy = "auth";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("RateLimiting").Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();

        return services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.AuthPermitLimit,
                    Window = TimeSpan.FromSeconds(options.AuthWindowSeconds),
                    QueueLimit = 0,
                }));
            limiter.OnRejected = async (context, ct) =>
            {
                var text = context.HttpContext.RequestServices.GetRequiredService<IApiText>();
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = text.Error("Title.TooManyRequests", "Too many requests."),
                        Detail = text.Error("Auth.TooManyAttempts", "Too many attempts. Wait a minute and try again."),
                    },
                });
            };
        });
    }
}
