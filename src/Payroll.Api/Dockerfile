# Build context: the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore first, from the project files only, so the layer is cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Payroll.Domain/Payroll.Domain.csproj src/Payroll.Domain/
COPY src/Payroll.Application/Payroll.Application.csproj src/Payroll.Application/
COPY src/Payroll.Infrastructure/Payroll.Infrastructure.csproj src/Payroll.Infrastructure/
COPY src/Payroll.Api/Payroll.Api.csproj src/Payroll.Api/
RUN dotnet restore src/Payroll.Api/Payroll.Api.csproj

COPY src/ src/
RUN dotnet publish src/Payroll.Api/Payroll.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Non-root user provided by the official .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Payroll.Api.dll"]
