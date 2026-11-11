using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Payroll.Api.ErrorHandling;
using Payroll.Application.Common;
using Payroll.Domain.Common;

namespace Payroll.Api.UnitTests.ErrorHandling;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task Validation_exception_returns_400_with_camel_case_field_errors_grouped_by_field()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("FullName", "'Full Name' must not be empty."),
            new ValidationFailure("FullName", "'Full Name' must be 100 characters or fewer."),
            new ValidationFailure("PayComponents[0].Amount", "'Amount' must be greater than '0'."),
        ]);

        var (status, body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("One or more validation errors occurred.", body.GetProperty("title").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal(2, errors.GetProperty("fullName").GetArrayLength());
        Assert.Equal("'Amount' must be greater than '0'.",
            errors.GetProperty("payComponents[0].amount")[0].GetString());
    }

    [Fact]
    public async Task Not_found_exception_returns_404_with_detail()
    {
        var (status, body) = await HandleAsync(new NotFoundException("Employee", 42));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("Employee 42 was not found.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Conflict_exception_for_a_field_returns_409_with_field_errors()
    {
        var (status, body) = await HandleAsync(ConflictException.ForField("email", "Email is already in use."));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("Email is already in use.", body.GetProperty("detail").GetString());
        Assert.Equal("Email is already in use.", body.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Conflict_exception_without_field_errors_returns_409_without_errors_member()
    {
        var (status, body) = await HandleAsync(new ConflictException("The record was modified by another user."));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("The record was modified by another user.", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Unauthorized_exception_returns_401_with_a_generic_detail()
    {
        var (status, body) = await HandleAsync(new UnauthorizedException());

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Equal("Invalid credentials.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Coded_errors_are_translated_to_the_request_language()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ar");
        try
        {
            var (status, body) = await HandleAsync(ConflictException.Coded(ErrorCodes.EmailInUse, "Email is already in use.", "email"));

            Assert.Equal(StatusCodes.Status409Conflict, status);
            Assert.Equal("البريد الإلكتروني مستخدم بالفعل.", body.GetProperty("detail").GetString());
            Assert.Equal("البريد الإلكتروني مستخدم بالفعل.", body.GetProperty("errors").GetProperty("email")[0].GetString());
            Assert.Equal("تعارض مع بيانات موجودة.", body.GetProperty("title").GetString());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Coded_validation_failures_are_translated_and_others_kept()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ar");
        try
        {
            var (_, body) = await HandleAsync(new ValidationException(
            [
                new ValidationFailure("HireDate", "Hire date cannot be in the future.") { ErrorCode = ErrorCodes.HireDateInFuture },
                new ValidationFailure("FullName", "already localized by FluentValidation"),
            ]));

            var errors = body.GetProperty("errors");
            Assert.Equal("تاريخ التعيين لا يمكن أن يكون في المستقبل.", errors.GetProperty("hireDate")[0].GetString());
            Assert.Equal("already localized by FluentValidation", errors.GetProperty("fullName")[0].GetString());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Forbidden_exception_returns_403()
    {
        var (status, body) = await HandleAsync(new ForbiddenException("You can only view your own payslip."));

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("You can only view your own payslip.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Domain_exception_returns_422_with_the_rule_as_detail()
    {
        var (status, body) = await HandleAsync(new DomainException("Employee is inactive."));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
        Assert.Equal("Employee is inactive.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Domain_error_with_a_code_is_translated_with_its_field_name()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ar");
        try
        {
            var (status, body) = await HandleAsync(new DomainException(DomainErrorCodes.Required, "FullName is required.", "FullName"));

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
            Assert.Equal("الاسم الكامل مطلوب.", body.GetProperty("detail").GetString());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_without_leaking_internals()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("connection string is secret-123"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("An unexpected error occurred.", body.GetProperty("title").GetString());
        Assert.False(body.TryGetProperty("detail", out _));
        Assert.DoesNotContain("secret-123", body.GetRawText());
    }

    [Fact]
    public async Task A_derived_exception_uses_the_mapper_of_its_base_type()
    {
        var (status, _) = await HandleAsync(new SpecificRuleViolation());

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
    }

    private sealed class SpecificRuleViolation() : DomainException("A more specific business rule.");

    [Fact]
    public async Task Every_problem_has_a_trace_id_and_problem_json_content_type()
    {
        var context = CreateHttpContext();

        await CreateHandler(context).TryHandleAsync(context, new DomainException("rule"), CancellationToken.None);

        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        var body = await ReadBodyAsync(context);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
    }

    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var context = CreateHttpContext();
        var handled = await CreateHandler(context).TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);
        return (context.Response.StatusCode, await ReadBodyAsync(context));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApiProblemDetails()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ApiExceptionHandler CreateHandler(HttpContext context) =>
        new(context.RequestServices.GetRequiredService<IProblemDetailsService>(),
            context.RequestServices.GetServices<IProblemMapper>(),
            context.RequestServices.GetRequiredService<Payroll.Api.Localization.IApiText>(),
            NullLogger<ApiExceptionHandler>.Instance);

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }
}
