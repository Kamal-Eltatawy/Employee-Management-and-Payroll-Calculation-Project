using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Auth;
using Payroll.Api.Configuration;
using Payroll.Api.ErrorHandling;
using Payroll.Api.OpenApi;
using Payroll.Application;
using Payroll.Infrastructure;
using Payroll.Infrastructure.Persistence;

DotEnvFile.LoadFromAncestors(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

// Structured logs: one JSON object per line, UTC timestamps (§16). No extra logging library.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services
    // FluentValidation owns required-field rules, so its camelCase error keys are the only ones the client sees.
    .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = ModelStateErrors.ToProblemResult);
builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401/403 from the auth middleware become problem+json too
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateAndSeedDevelopmentDataAsync();
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Payroll API v1"));
}

app.MapControllers();

// Anonymous; returns only "Healthy" / "Unhealthy", no details (§16).
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

public partial class Program;
