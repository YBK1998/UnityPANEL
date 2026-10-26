@echo off
REM ---------------------------------------------------------------------------
REM Build PanelWireViewer with the installed Visual Studio 2022 + vcpkg.
REM vcpkg is in manifest mode (vcpkg.json), so dependencies (imgui, glfw3,
REM sqlite3) are fetched/built automatically on the first configure.
REM ---------------------------------------------------------------------------
setlocal enabledelayedexpansion

REM Always build from this script's own directory, regardless of caller cwd.
cd /d "%~dp0"

set VCPKG_ROOT=C:\vcpkg
if not exist "%VCPKG_ROOT%\scripts\buildsystems\vcpkg.cmake" (
    echo ERROR: vcpkg toolchain not found at %VCPKG_ROOT%.
    echo Edit VCPKG_ROOT in build.bat to point at your vcpkg install.
    exit /b 1
)

REM --- Locate CMake (ships inside Visual Studio; usually not on PATH) ---------
set "CMAKE="
where cmake >nul 2>nul && set "CMAKE=cmake"
if not defined CMAKE (
    set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
    for /f "usebackq delims=" %%i in (`"!VSWHERE!" -latest -property installationPath`) do (
        set "CAND=%%i\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
        if exist "!CAND!" set "CMAKE=!CAND!"
    )
)
if not defined CMAKE (
    echo ERROR: cmake.exe not found. Open a "Developer Command Prompt for VS" and run build.bat there.
    exit /b 1
)
echo Using CMake: !CMAKE!

if not exist build mkdir build

"%CMAKE%" -S . -B build -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%\scripts\buildsystems\vcpkg.cmake
if errorlevel 1 exit /b 1

"%CMAKE%" --build build --config Release
if errorlevel 1 exit /b 1

echo.
echo ===========================================================
echo  Built: build\Release\PanelWireViewer.exe
echo  Run it from this folder so panelwire.db lives alongside it.
echo ===========================================================
