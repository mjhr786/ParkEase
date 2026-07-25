@echo off
REM ========================================
REM ParkEase - Azure Deployment Script
REM Builds and packages frontend + backend as single app
REM PR-01: win-x64 RID, no symbols, clean SPA assets
REM ========================================

echo.
echo ========================================
echo Building ParkEase for Azure Deployment
echo ========================================
echo.

set ROOT_DIR=%~dp0
set FRONTEND_DIR=%ROOT_DIR%frontend
set BACKEND_DIR=%ROOT_DIR%backend
set API_PROJECT=%BACKEND_DIR%\src\ParkingApp.API
set API_WWWROOT=%API_PROJECT%\wwwroot
set PUBLISH_DIR=%ROOT_DIR%publish
set RUNTIME_ID=win-x64

echo [1/6] Cleaning previous package output...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"

echo [2/6] Building frontend...
cd /d "%FRONTEND_DIR%"
call npm ci
if errorlevel 1 goto :error
call npm run build
if errorlevel 1 goto :error

echo [3/6] Refreshing API wwwroot SPA assets...
if exist "%API_WWWROOT%\assets" rmdir /s /q "%API_WWWROOT%\assets"
if not exist "%API_WWWROOT%" mkdir "%API_WWWROOT%"
xcopy "%FRONTEND_DIR%\dist\*" "%API_WWWROOT%\" /E /Y /Q
if errorlevel 1 goto :error

echo [4/6] Publishing backend (RID=%RUNTIME_ID%)...
cd /d "%BACKEND_DIR%"
dotnet publish "%API_PROJECT%" -c Release -r %RUNTIME_ID% --self-contained false -o "%PUBLISH_DIR%" ^
  /p:DebugType=None /p:DebugSymbols=false ^
  /p:CopyOutputSymbolsToPublishDirectory=false ^
  /p:CopyDebugSymbolFilesFromPackages=false
if errorlevel 1 goto :error

echo [5/6] Stripping residual symbol files...
powershell -NoProfile -Command "Get-ChildItem -LiteralPath '%PUBLISH_DIR%' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.pdb','.dbg','.dwarf' -or $_.Name -like '*.dylib.dwarf' } | Remove-Item -Force -ErrorAction SilentlyContinue"

echo [6/6] Creating deployment package...
cd /d "%PUBLISH_DIR%"
powershell -NoProfile -Command "Compress-Archive -Path * -DestinationPath '%ROOT_DIR%parkease-deploy.zip' -Force"

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Deployment package: %ROOT_DIR%parkease-deploy.zip
echo Publish folder:     %PUBLISH_DIR%
echo.
echo To deploy to Azure:
echo   1. Azure Portal ^> Web App ^> Deployment Center / ZIP Deploy
echo   2. Upload parkease-deploy.zip
echo.
echo Or Azure CLI:
echo   az webapp deployment source config-zip --resource-group YOUR_RG --name YOUR_APP --src parkease-deploy.zip
echo.
echo See docs\deploy-publish.md for FileZilla / free-tier notes.
echo.
pause
exit /b 0

:error
echo.
echo BUILD FAILED.
pause
exit /b 1
