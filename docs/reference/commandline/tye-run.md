# tye run

## Name

`tye run` - Runs the application.

## Synopsis

```text
tye run [-?|-h|--help] [--no-build] [--port <port>] [--logs <logs>] [--dtrace <trace>] [--metrics <metrics>] [--debug <service>] [--docker] [--dashboard] [--watch <service>] [-f|--framework <framework>] [--tags <tags>] [-v|--verbosity <Debug|Info|Quiet>] [<PATH>]
```

## Description

The `tye run` command will run an application locally. `tye run` by default will:

- Start all services/projects in the application.
- Start a dashboard at <http://localhost:8000> to view all services running in the application.

## Arguments

`PATH`

The path to either a file or directory to run `tye run` on. Can either be a yaml, sln, or project file.

If a directory path is specified, `tye run` will default to using these files, in the following order:

- `tye.yaml`
- `*.sln`
- `*.csproj/*.fsproj`

## Options

- `--no-build`

    Does not build projects before running.

- `--port <port>`

    The port to run the dashboard on. Defaults to port 8000 if not specified.

- `--logs <logs>`

    Write structured application logs to the specified log providers. Supported providers are console, elastic (Elasticsearch), ai (ApplicationInsights), seq.

- `--dtrace <trace>`

    Write distributed traces to the specified providers. Supported providers are zipkin.

- `--debug <service>`

    Waits for debugger attach to dotnet service. Specify `*` to wait to attach to all dotnet services.

- `--docker`

    Run projects as docker containers.

- `--dashboard`

    Launch dashboard on run.

- `--watch <service>`

    Watches for file changes in a dotnet project that is built by tye. Uses [`dotnet watch`](https://docs.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-3.1) to monitor for file changes. Specify `*` to watch all dotnet services.

- `-f|--framework <framework>`

    The target framework hint to use for all cross-targeting projects with multiple TFMs. This value must be a valid target framework for each individual cross-targeting project. Non-crosstargeting projects will ignore this hint and the value TFM configured in tye.yaml will override this hint.

- `--tags <tags>`

    Filter the group of running services by tag.

- `--verbosity <verbosity>`

    Sets the output verbosity of the process.
    Possible values are

    * `debug` - display all logs that the process outputs
    * `info` - display only informational logs
    * `quiet` - display only warnings and errors

    The default value is `info`

## Examples

- Run an application in the current directory:

    ```text
    tye run
    ```

- Run an application where the dashboard is hosted on another port:

    ```text
    tye run --port 5050
    ```

- Run an application and wait for all projects to debug attach:

    ```text
    tye run --debug *
    ```
