# Builds and starts everything in Docker (db + API + Angular in English and Arabic), waits until it answers,
# then prints every address. Usage:  .\start.ps1        Stop:  docker compose down
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Test-Path .env)) {
    Write-Host 'No .env file: copy .env.example to .env and set the values first.' -ForegroundColor Yellow
    exit 1
}

docker compose up -d --build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Waiting for the database, the API and the web app...' -NoNewline
$deadline = (Get-Date).AddMinutes(5)
do {
    Start-Sleep -Seconds 3
    Write-Host '.' -NoNewline
    $status = docker inspect --format '{{.State.Health.Status}}' payroll-web-1 2>$null
} until ($status -eq 'healthy' -or (Get-Date) -gt $deadline)
Write-Host ''

if ($status -ne 'healthy') {
    Write-Host 'Not ready after 5 minutes. Check: docker compose logs api web' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '================ Payroll is ready ================' -ForegroundColor Green
Write-Host '  App (English)  http://localhost:8080/en/'
Write-Host '  App (Arabic)   http://localhost:8080/ar/'
Write-Host '  Swagger UI     http://localhost:5081/swagger'
Write-Host '  Health         http://localhost:8080/health'
Write-Host '  SQL Server     localhost,14333 (user sa, password from .env)'
Write-Host '==================================================' -ForegroundColor Green
