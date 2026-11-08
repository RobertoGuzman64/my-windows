@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\mw.ps1" %*
exit /b %ERRORLEVEL%
