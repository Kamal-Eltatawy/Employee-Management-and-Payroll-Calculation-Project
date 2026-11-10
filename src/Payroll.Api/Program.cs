using System.Text.Json.Serialization;
using Payroll.Api.Configuration;
using Payroll.Api.ErrorHandling;

DotEnvFile.LoadFromAncestors(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Payroll API v1"));
}

app.MapControllers();
app.Run();

public partial class Program;
