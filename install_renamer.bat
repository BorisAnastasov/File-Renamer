@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "APPNAME=Renamer"
set "EXE=Renamer.exe"
set "INSTALL_DIR=%LocalAppData%\%APPNAME%"

echo.
echo Installing %APPNAME% to: "%INSTALL_DIR%"
echo.

if not exist "%~dp0%EXE%" (
  echo [ERROR] "%EXE%" not found next to this installer: %~dp0
  echo Make sure install_renamer.bat and %EXE% are in the same folder.
  echo.
  pause
  exit /b 1
)

mkdir "%INSTALL_DIR%" 2>nul
copy /Y "%~dp0%EXE%" "%INSTALL_DIR%\%EXE%" >nul

REM Read current user PATH
for /f "tokens=2* delims= " %%A in ('reg query HKCU\Environment /v Path 2^>nul ^| find "Path"') do set "USERPATH=%%B"

REM Add install dir to PATH if missing
echo %USERPATH% | find /I "%INSTALL_DIR%" >nul
if errorlevel 1 (
  echo Adding "%INSTALL_DIR%" to your user PATH...
  if defined USERPATH (
    set "NEWPATH=%USERPATH%;%INSTALL_DIR%"
  ) else (
    set "NEWPATH=%INSTALL_DIR%"
  )
  setx Path "%NEWPATH%" >nul
) else (
  echo PATH already includes install directory. Skipping PATH update.
)

REM Create a small shim so "renamer" works without typing .exe
> "%INSTALL_DIR%\renamer.cmd" echo @"%INSTALL_DIR%\%EXE%" %%*

echo.
echo Done!
echo Open a NEW terminal and run:  renamer --help
echo.
pause
