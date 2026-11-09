@echo off

echo [UMK] Removing previous global installation (if present)...
dotnet tool uninstall -g KRWCLASSIC.UMK >nul 2>&1

echo [UMK] Done!

