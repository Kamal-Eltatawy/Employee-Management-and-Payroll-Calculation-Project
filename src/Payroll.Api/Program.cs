using System.Text.Json.Serialization;
using Payroll.Api.Configuration;
using Payroll.Api.ErrorHandling;
using Payroll.Application;
using Payroll.Infrastructure;

DotEnvFile.LoadFromAncestors(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

builder.Services
    // FluentValidation owns required-field rules, so its camelCase error keys are the only ones the client sees.
    .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateAndSeedDevelopmentDataAsync();
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Payroll API v1"));
}

app.MapControllers();
app.Run();

public partial class Program;
