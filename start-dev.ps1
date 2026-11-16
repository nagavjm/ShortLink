<#
.SYNOPSIS
    Starts both the ShortLink .NET 8 Backend API and Angular 19 Frontend UI.
.DESCRIPTION
    Checks local prerequisites (PostgreSQL & Redis), spawns the backend and frontend
    in dedicated terminal windows, and displays live endpoint URLs.
#>

$ErrorActionPreference = "Continue"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "       ShortLink: Scalable Full-Stack URL Shortener            " -ForegroundColor Yellow
Write-Host "            ASP.NET Core (.NET 8) + Angular 19                 " -ForegroundColor White
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check PostgreSQL (Azure or Local)
$pgHost = "psql-shortlink-prod.postgres.database.azure.com"
$pgTest = Test-NetConnection -ComputerName $pgHost -Port 5432 -WarningAction SilentlyContinue -InformationLevel Quiet
if ($pgTest) {
    Write-Host "[OK] Azure PostgreSQL ($pgHost:5432) is reachable." -ForegroundColor Green
} else {
    $pgLocal = Test-NetConnection -ComputerName "127.0.0.1" -Port 5432 -WarningAction SilentlyContinue -InformationLevel Quiet
    if ($pgLocal) {
        Write-Host "[OK] Local PostgreSQL (127.0.0.1:5432) is reachable." -ForegroundColor Green
    } else {
        Write-Host "[WARNING] PostgreSQL is not reachable on Azure or localhost." -ForegroundColor Yellow
    }
}

# 2. Check Redis (Azure Managed Redis or Local)
$redisHost = "redis-shortlink-prod.westus3.redis.azure.net"
$redisTest = Test-NetConnection -ComputerName $redisHost -Port 10000 -WarningAction SilentlyContinue -InformationLevel Quiet
if ($redisTest) {
    Write-Host "[OK] Azure Managed Redis ($redisHost:10000) is reachable." -ForegroundColor Green
} else {
    $redisLocal = Test-NetConnection -ComputerName "127.0.0.1" -Port 6379 -WarningAction SilentlyContinue -InformationLevel Quiet
    if ($redisLocal) {
        Write-Host "[OK] Local Redis (127.0.0.1:6379) is reachable." -ForegroundColor Green
    } else {
        Write-Host "[WARNING] Redis is not reachable on Azure or localhost." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Starting processes..." -ForegroundColor White

# 3. Start Backend API in a separate PowerShell window
$apiCommand = "cd '$PSScriptRoot'; `$host.UI.RawUI.WindowTitle = 'ShortLink Backend API (.NET 8)'; Write-Host 'Starting ShortLink.API on http://localhost:5050...' -ForegroundColor Cyan; dotnet run --project src/ShortLink.API --launch-profile http"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $apiCommand

# 4. Start Angular UI in a separate PowerShell window
$uiCommand = "cd '$PSScriptRoot/client'; `$host.UI.RawUI.WindowTitle = 'ShortLink Frontend (Angular 19)'; Write-Host 'Starting Angular Dev Server on http://localhost:4200...' -ForegroundColor Green; npm start"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $uiCommand

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " All services have been launched in dedicated windows:" -ForegroundColor White
Write-Host "   - Frontend UI:     http://localhost:4200" -ForegroundColor Green
Write-Host "   - Swagger UI:      http://localhost:5050/swagger" -ForegroundColor Cyan
Write-Host "   - Health (Ready):  http://localhost:5050/health/ready" -ForegroundColor Yellow
Write-Host "   - Health (Live):   http://localhost:5050/health/live" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To stop the application, close the two opened terminal windows." -ForegroundColor Gray
