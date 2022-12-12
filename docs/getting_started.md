# Getting Started

Tye is a tool that makes developing, testing, and deploying microservices and distributed applications easier. Project Tye includes a local orchestrator to make developing microservices easier and the ability to deploy microservices to Kubernetes with minimal configuration.

## Installing Tye

1. Install [.NET 6.0](<https://dot.net>).

    > Tye currently requires .NET 6 but earlier releases (`0.10.0` and earlier) require .NET Core 3.1.

1. Install tye via the following command:

    ```text
    dotnet tool install -g Microsoft.Tye --version "0.11.0-alpha.22111.1"
    ```

    OR if you already have Tye installed and want to update:

    ```text
    dotnet tool update -g Microsoft.Tye --version "0.11.0-alpha.22111.1"
    ```

    > If using earlier versions of Tye on Mac with both `arm64` and `x64` .NET SDKs, you may need to supply the `-a x64` parameter when installing Tye as those versions require the x64 version of .NET Core 3.1.
    >
    > Example:
    >
    > ```
    > dotnet tool install -a x64 -g Microsoft.Tye --version "0.10.0-alpha.21420.1
    > ```

    > If using Mac and, if getting "command not found" errors when running `tye`, you may need to ensure that the `$HOME/.dotnet/tools` directory has been added to `PATH`.
    >
    > For example, add the following to the end of your `~/.zshrc` or `~/.zprofile`:
    >
    > ```
    > # Add .NET global tools (like Tye) to PATH.
    > export PATH=$HOME/.dotnet/tools:$PATH
    > ```

## Tye VSCode extension

Install the [Tye Visual Studio Code extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-tye).

## Next steps

1. Once tye is installed, continue to the [Basic Tutorial](/docs/tutorials/hello-tye/00_run_locally.md).
1. Check out additional samples for more advanced concepts, such as using redis, rabbitmq, and service discovery.

## Working with CI builds

This will install the newest available build from our CI.

```txt
dotnet tool install -g Microsoft.Tye --version "0.12.0-*" --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json
```

If you already have a build installed and you want to update, replace `install` with `update`:

```txt
dotnet tool update -g Microsoft.Tye --version "0.12.0-*" --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json
```

> :bulb: Note that the version numbers for our CI builds and released packages will usually be different.

If you are using CI builds of Tye we also recommend using CI builds of our libraries as well with the matching version. To add the `dotnet-core` package source add a `NuGet.config` to your repository or solution directory.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="dotnet6" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```
