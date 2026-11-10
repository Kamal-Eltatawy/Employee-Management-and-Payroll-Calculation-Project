# Starts everything in Docker (db + API + Angular in English and Arabic), waits until it answers,
# then prints every address.
#   .\start.ps1                 build the images from the source here
#   .\start.ps1 -FromRegistry   run the images CI published to GitHub Container Registry (IMAGE_PREFIX in .env)
# Stop:  docker compose down
param([switch]$FromRegistry)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Test-Path .env)) {
    Write-Host 'No .env file: copy .env.example to .env and set the values first.' -ForegroundColor Yellow
    exit 1
}

if ($FromRegistry) {
    docker compose -f docker-compose.yml -f docker-compose.prod.yml pull api web
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --no-build
} else {
    docker compose up -d --build
}
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
