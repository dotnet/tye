# Getting Started

Tye is a tool that makes developing, testing, and deploying microservices and distributed applications easier. Project Tye includes a local orchestrator to make developing microservices easier and the ability to deploy microservices to Kubernetes with minimal configuration.

## Installing tye

1. Install [.NET Core 3.1](<http://dot.net>).
2. Install tye via the following command:

    ```text
    dotnet tool install -g Microsoft.Tye --version "0.1.0-alpha.20168.8" --add-source https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
    ```

    OR

    ```text
    dotnet tool update -g Microsoft.Tye --version "0.1.0-alpha.20168.8" --add-source https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
    ```

3. Verify the installation was complete by running:

    ```
    tye --version
    > 0.1.0-alpha.20168.8+f45b9444d4894009bde48e2a3411a52dd09497b1
    ```

## Next steps

1. Once tye is installed, continue to the [Frontend-Backend sample](frontend_backend_run.md).
2. Check out additional samples for more advanced concepts, such as using redis, rabbitmq, and service discovery.


## Working with CI builds

This will install the newest available build from our CI.

```txt
dotnet tool install -g Microsoft.Tye --version "0.1.0-*" --add-source https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
```