@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "APPNAME=Renamer"
set "INSTALL_DIR=%LocalAppData%\%APPNAME%"

echo.
echo Uninstalling %APPNAME% from "%INSTALL_DIR%" ...
del /q "%INSTALL_DIR%\Renamer.exe" "%INSTALL_DIR%\renamer.cmd" 2>nul
rd "%INSTALL_DIR%" 2>nul

for /f "tokens=2* delims= " %%A in ('reg query HKCU\Environment /v Path 2^>nul ^| find "Path"') do set "USERPATH=%%B"
set "NEWPATH=%USERPATH%"
set "NEWPATH=%NEWPATH:%INSTALL_DIR%;=%"
set "NEWPATH=%NEWPATH:;%INSTALL_DIR%=%"
set "NEWPATH=%NEWPATH:;;=;%"

setx Path "%NEWPATH%" >nul

echo.
echo Uninstalled. Open a NEW terminal to refresh PATH.
echo.
pause
