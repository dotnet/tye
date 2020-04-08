# tye run

## Name

`tye run` - Runs the application.

## Synopsis 

```text
tye run [-?|-h|--help] [--no-build] [--port <port>] [--dtrace] [--debug][-f|--force] [<PATH>]
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

- `--logs`

    Write structured application logs to the specified log providers. Supported providers are console, elastic (Elasticsearch), ai (ApplicationInsights), seq.

- `--dtrace <logs>`  

    Write distributed traces to the specified providers. Supported providers are zipkin.

- `--debug <service>`

    Waits for debugger attach to service. Specify `*` to wait to attach to all services.

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
