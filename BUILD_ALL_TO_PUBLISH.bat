@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0"

echo ==========================================
echo BUILD ALL PROJECTS ^(Release^)
echo ==========================================

dotnet build AgeLanServer.slnx -c Release -nologo
if errorlevel 1 (
  echo.
  echo [ERROR] Build failed.
  exit /b 1
)

echo.
echo ==========================================
echo PUBLISH ALL EXECUTABLE PROJECTS
echo ==========================================

call :Publish "AgeLanServer.Server\AgeLanServer.Server.csproj" "publish\server"
call :Publish "AgeLanServer.Launcher\AgeLanServer.Launcher.csproj" "publish\launcher"
call :Publish "AgeLanServer.ServerGenCert\AgeLanServer.ServerGenCert.csproj" "publish\genCert"
call :Publish "AgeLanServer.LauncherAgent\AgeLanServer.LauncherAgent.csproj" "publish\agent"
call :Publish "AgeLanServer.LauncherConfig\AgeLanServer.LauncherConfig.csproj" "publish\config"
call :Publish "AgeLanServer.LauncherConfigAdmin\AgeLanServer.LauncherConfigAdmin.csproj" "publish\config-admin"
call :Publish "AgeLanServer.LauncherConfigAdminAgent\AgeLanServer.LauncherConfigAdminAgent.csproj" "publish\config-admin-agent"
call :Publish "AgeLanServer.BattleServerManager\AgeLanServer.BattleServerManager.csproj" "publish\battle-server-manager"
call :Publish "AgeLanServer.BattleServerBroadcast\AgeLanServer.BattleServerBroadcast.csproj" "publish\battle-server-broadcast"

if errorlevel 1 exit /b 1

echo.
echo ==========================================
echo SYNC SUBFOLDER OUTPUTS TO publish ROOT
echo ==========================================

for %%D in (
  server
  launcher
  genCert
  agent
  config
  config-admin
  config-admin-agent
  battle-server-manager
  battle-server-broadcast
) do (
  if exist "publish\%%D" (
    for %%F in ("publish\%%D\*") do (
      if not exist "%%~fF\" (
        copy /Y "%%~fF" "publish\" >nul
      )
    )
  )
)

echo.
echo [OK] Build + publish completed.
exit /b 0

:Publish
set "PROJECT=%~1"
set "OUTDIR=%~2"

echo.
echo [PUBLISH] %PROJECT% ^> %OUTDIR%

dotnet publish "%PROJECT%" -c Release -o "%OUTDIR%" -nologo
if errorlevel 1 (
  echo [ERROR] Publish failed: %PROJECT%
  exit /b 1
)

goto :eof
