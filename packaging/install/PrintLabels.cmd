@echo off
setlocal EnableExtensions
set "EXE_DIR=%~dp0"
set "LIST=%TEMP%\dnl-%RANDOM%-%RANDOM%.pdflist"
if exist "%LIST%" del /f /q "%LIST%"

:collect
if "%~1"=="" goto launch
>>"%LIST%" echo("%~1")
shift
goto collect

:launch
if not exist "%LIST%" exit /b 0
"%EXE_DIR%DeliveryNoteLabeler.exe" --open-from "%LIST%"
set "ERR=%ERRORLEVEL%"
del /f /q "%LIST%" 2>nul
exit /b %ERR%
