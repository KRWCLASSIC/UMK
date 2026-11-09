@echo off
setlocal

echo [UMK] Packing KRWCLASSIC.UMK (Release)...
dotnet pack "%~dp0UMK.csproj" -c Release
if %ERRORLEVEL% neq 0 (
    echo [UMK] Error: dotnet pack failed.
    exit /b %ERRORLEVEL%
)

echo [UMK] Removing previous global installation (if present)...
dotnet tool uninstall -g KRWCLASSIC.UMK >nul 2>&1

echo [UMK] Installing KRWCLASSIC.UMK globally...
dotnet tool install -g --add-source "%~dp0bin\Release" KRWCLASSIC.UMK
if %ERRORLEVEL% neq 0 (
    echo [UMK] Error: Global tool installation failed.
    exit /b %ERRORLEVEL%
)

echo [UMK] Done! You can now run 'umk' from any terminal.

