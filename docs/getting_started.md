# Getting Started

Tye is a tool that makes developing, testing, and deploying microservices and distributed applications easier. Project Tye includes a local orchestrator to make developing microservices easier and the ability to deploy microservices to Kubernetes with minimal configuration.

## Installing tye

1. Install [.NET Core 3.1.](<http://dot.net>).
1. Install tye via the following command:

    ```text
    dotnet tool install -g tye --version 0.1.0-alpha.20161.4 --interactive --add-source https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal/nuget/v3/index.json
    ```

1. Verify the installation was complete by running:

    ```
    tye --version
    > 0.1.0-alpha.20161.4+d69009b73074973484b1602011dbb0c730f013bf
    ```

## Next steps

1. Once tye is installed, continue to the [Frontend-Backend sample](frontend_backend_run.md).
2. Check out additional samples for more advanced concepts, such as using redis, rabbitmq, and service discovery.
