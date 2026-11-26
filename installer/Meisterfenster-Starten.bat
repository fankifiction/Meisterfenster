@echo off
setlocal EnableExtensions
title Meisterfenster
echo.
echo Meisterfenster wird auf diesen Computer kopiert ...
echo Bitte warten, das dauert ein paar Sekunden.
echo.
echo NICHT die Datei Wochenplanung.Presentation.exe direkt von der
echo Freigabe starten - sonst bleibt oft nur der Ladekreis.
echo.
set "SRC=%~dp0"
set "DST=%LOCALAPPDATA%\Wochenplanung\app"
if not exist "%DST%" mkdir "%DST%"
xcopy "%SRC%*" "%DST%\" /E /I /Y /Q
if errorlevel 1 (
  echo.
  echo Kopieren fehlgeschlagen.
  echo Losung: Ordner auf diesen PC kopieren und dort starten,
  echo oder Meisterfenster_Setup.exe auf diesem PC installieren.
  echo.
  pause
  exit /b 1
)
echo Fertig. Programm startet.
start "" "%DST%\Wochenplanung.Presentation.exe"
endlocal
