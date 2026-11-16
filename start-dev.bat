@echo off
title ShortLink Full Stack Launcher
echo ================================================================
echo        ShortLink: Scalable Full-Stack URL Shortener
echo             ASP.NET Core (.NET 8) + Angular 19
echo ================================================================
echo.
echo [1/2] Starting ASP.NET Core Web API on http://localhost:5050 ...
start "ShortLink Backend API (.NET 8)" powershell -NoExit -Command "dotnet run --project src/ShortLink.API --launch-profile http"

echo [2/2] Starting Angular Dev Server on http://localhost:4200 ...
start "ShortLink Frontend (Angular 19)" powershell -NoExit -Command "cd client; npm start"

echo.
echo ================================================================
echo  All services have been launched in separate terminal windows:
echo    - Frontend UI:     http://localhost:4200
echo    - Swagger UI:      http://localhost:5050/swagger
echo    - Health (Ready):  http://localhost:5050/health/ready
echo ================================================================
pause
