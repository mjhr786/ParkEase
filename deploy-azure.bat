@echo off
REM ========================================
REM ParkEase - Azure Deployment Script
REM Builds and packages frontend + backend as single app
REM ========================================

echo.
echo ========================================
echo Building ParkEase for Azure Deployment
echo ========================================
echo.

REM Set paths
set ROOT_DIR=%~dp0
set FRONTEND_DIR=%ROOT_DIR%frontend
set BACKEND_DIR=%ROOT_DIR%backend
set API_PROJECT=%BACKEND_DIR%\src\ParkingApp.API
set PUBLISH_DIR=%ROOT_DIR%publish
set WWWROOT_DIR=%PUBLISH_DIR%\wwwroot

REM Clean previous build
echo [1/5] Cleaning previous build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"

REM Build frontend
echo [2/5] Building frontend...
cd "%FRONTEND_DIR%"
call npm ci
call npm run build

REM Build backend
echo [3/5] Building backend...
cd "%BACKEND_DIR%"
dotnet publish "%API_PROJECT%" -c Release -o "%PUBLISH_DIR%"

REM Copy frontend to wwwroot
echo [4/5] Copying frontend to wwwroot...
if not exist "%WWWROOT_DIR%" mkdir "%WWWROOT_DIR%"
xcopy "%FRONTEND_DIR%\dist\*" "%WWWROOT_DIR%\" /E /Y /Q

REM Create zip for deployment
echo [5/5] Creating deployment package...
cd "%PUBLISH_DIR%"
powershell Compress-Archive -Path * -DestinationPath "%ROOT_DIR%parkease-deploy.zip" -Force

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Deployment package: %ROOT_DIR%parkease-deploy.zip
echo.
echo To deploy to Azure:
echo   1. Go to Azure Portal ^> Your Web App ^> Deployment Center
echo   2. Choose "Local Git" or "ZIP Deploy"
echo   3. Upload parkease-deploy.zip
echo.
echo Or use Azure CLI:
echo   az webapp deployment source config-zip --resource-group YOUR_RG --name YOUR_APP --src parkease-deploy.zip
echo.
pause
