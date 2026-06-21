@echo off
rem ── ReadySetGrow (3D) launcher ───────────────────────────────────────────
rem Builds the C# project, then runs the 3D game (no editor needed).
cd /d "%~dp0"

set "GODOT=%USERPROFILE%\Downloads\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"

echo Building...
dotnet build GrowDaGarden.csproj -c Debug || goto :error

echo Launching ReadySetGrow...
"%GODOT%" --path "%~dp0"
goto :eof

:error
echo.
echo Build failed. Open project.godot in the Godot 4.7 .NET editor instead.
pause
