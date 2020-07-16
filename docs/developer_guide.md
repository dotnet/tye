# Developer Guide

## Building the code

Building tye from source allows you to experiment, and to contribute your improvements back to the project.

## Install pre-requisites

Tye uses [Arcade](https://github.com/dotnet/Arcade) to build. As of right now there are no prerequisites needed to build and edit the code.

We recommend using whatever developer tools you like for C# and .NET development. Visual Studio has [free versions](https://visualstudio.microsoft.com/downloads/) and VS Code has an available C# extension.

Your development environment will likely require the [.NET SDK](https://dotnet.microsoft.com/download) to be installed, though the command line build will use a local copy of .NET.

### Integration test pre-requisites

Our integration tests will make use of Docker's command line tools for some functionality. You can find the appropriate installer for Docker [here](https://hub.docker.com/search?q=&type=edition&offering=community&sort=updated_at&order=desc).

## Clone the source code

For a new copy of the project, run:

```sh
git clone https://github.com/dotnet/tye
```

## Opening in Visual Studio

Before opening our .sln files in Visual Studio or VS Code, we recommend performing the following actions.

1. Executing the following on command-line:

   ```ps1
   .\build.cmd
   ```

   This will download the required tools and build the entire repository once. At that point, you should be able to open .sln files to work on the projects you care about.

   > :bulb: Pro tip: you will also want to run this command after pulling large sets of changes. On the master branch, we regularly update the versions of .NET Core SDK required to build the repo.
   > You will need to restart your editor every time we update the .NET Core SDK.

2. Use the `startvs.cmd` script to open Visual Studio .sln files. This script first sets the required environment variables.

## Opening in Visual Studio Code

Using Visual Studio Code with this repo requires setting environment variables on command line first.
Use these command to launch VS Code with the right settings.

On Windows (requires PowerShell):

```ps1
# The extra dot at the beginning is required to 'dot source' this file into the right scope.

. .\activate.ps1
code .
```

On macOS/Linux:

```bash
source activate.sh
code .
```

Note that if you are using the "Remote-WSL" extension in VSCode, the environment is not supplied
to the process in WSL.  You can workaround this by explicitly setting the environment variables
in `~/.vscode-server/server-env-setup`.
See https://code.visualstudio.com/docs/remote/wsl#_advanced-environment-setup-script for details.

## Building on command-line

You can also build the entire project on command line with the `build.cmd`/`.sh` scripts.

On Windows:

```ps1
.\build.cmd
```

On macOS/Linux:

```bash
./build.sh
```

### Using `dotnet` on command line in this repo

Because we are using a local version of .NET Core, you have to set a handful of environment variables
to make the .NET Core command line tool work well. You can set these environment variables like this

On Windows (requires PowerShell):

```ps1
# The extra dot at the beginning is required to 'dot source' this file into the right scope.

. .\activate.ps1
```

On macOS/Linux:

```bash
source ./activate.sh
```

## Running tests on command-line

Tests are not run by default. Use the `-test` option to run tests in addition to building.

On Windows:

```ps1
.\build.cmd -test
```

On macOS/Linux:

```bash
./build.sh --test
```

## Using local builds

The easiest way to use a custom build of tye is to `dotnet run -p <path to tye project>`.

If you want to install your build as a dotnet global tool, that is possible as well with the following steps:

1. Building the repo and create packages in the artifacts folder that can be used

On Windows:

```ps1
.\build.cmd -pack
```

On macOS/Linux:

```bash
./build.sh --pack
```

2. Install the package

```sh
dotnet tool install microsoft.tye -g --version "0.4.0-dev" --add-source ./artifacts/packages/Debug/Shipping
```
