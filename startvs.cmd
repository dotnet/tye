@ECHO OFF

:: This command launches a Visual Studio solution with environment variables required to use a local version of the .NET Core SDK.

:: This tells .NET Core to use .dotnet\dotnet.exe
SET DOTNET_ROOT=%~dp0.dotnet\

:: This tells .NET Core not to go looking for .NET Core in other places
SET DOTNET_MULTILEVEL_LOOKUP=0

:: Put our local dotnet.exe on PATH first so Visual Studio knows which one to use
SET PATH=%DOTNET_ROOT%;%PATH%

SET sln=%~1

IF "%sln%"=="" (
    SET sln=tye.sln
)

IF NOT EXIST "%DOTNET_ROOT%\dotnet.exe" (
    echo .NET Core has not yet been installed. Running restore.cmd to install it.
    call restore.cmd
)

start "" "%sln%"
