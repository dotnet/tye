# Getting Started

Tye is a tool that makes developing, testing, and deploying microservices and distributed applications easier. Project Tye includes a local orchestrator to make developing microservices easier and the ability to deploy microservices to Kubernetes with minimal configuration.

## Installing tye

1. Install [.NET Core 3.1](<http://dot.net>).
2. Install tye via the following command:

    ```text
    dotnet tool install -g Microsoft.Tye --version "0.6.0-alpha.21070.5"
    ```

    OR if you already have Tye installed and want to update:

    ```text
    dotnet tool update -g Microsoft.Tye --version "0.6.0-alpha.21070.5"
    ```

## Next steps

1. Once tye is installed, continue to the [Basic Tutorial](/docs/tutorials/hello-tye/00_run_locally.md).
2. Check out additional samples for more advanced concepts, such as using redis, rabbitmq, and service discovery.


## Working with CI builds

This will install the newest available build from our CI.

```txt
dotnet tool install -g Microsoft.Tye --version "0.7.0-*" --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json
```

If you already have a build installed and you want to update, replace `install` with `update`:

```txt
dotnet tool update -g Microsoft.Tye --version "0.7.0-*" --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json
```

> :bulb: Note that the version numbers for our CI builds and released packages will usually be different.

If you are using CI builds of Tye we also recommend using CI builds of our libraries as well with the matching version. To add the `dotnet-core` package source add a `NuGet.config` to your repository or solution directory.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="dotnet5" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```
