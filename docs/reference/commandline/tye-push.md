# tye push

## Name

`tye push` - Builds the application's containers and pushes them to the container registry.

## Synopsis

```text
tye push [-?|-h|--help] [-i|--interactive] [-v|--verbosity <Debug|Info|Quiet>] [--tags <tags>] [-f|--framework <framework>] [--force]  [<PATH>]
```

## Description

The `tye push` command will build all of an application's project services into containers and push the containers to a remote registry without deploying to Kubernetes.

This command is useful if you want to use Tye to containerize .NET projects and manage deployment separately.

> :warning: The `tye push` command requires access to a remote container registry. Images will be tagged using the registry configured in `tye.yaml` (if present), or using a registry supplied interactively at the command line.

> :bulb: The `tye push` command uses Docker's credentials for pushing to the remote container registry. Make sure Docker is configured to push to your registry before running `tye push`.

## Arguments

`PATH`

The path to either a file or directory to execute `tye push` on. Can either be a yaml, sln, or project file, however it is recommend to have a tye.yaml file for `tye push`.

If a directory path is specified, `tye push` will default to using these files, in the following order:

- `tye.yaml`
- `*.sln`
- `*.csproj/*.fsproj`

## Options

- `-i|--interactive`

    Does an interactive deployment that will accept input for values that are required by default.

- `-v|--verbosity <Debug|Info|Quiet>`

    The verbosity of logs emitted by `tye build-push-deploy`. Defaults to Info.

- `--tags <tags>`

    Filter the group of running services by tag.

- `-f|--framework <framework>`

    The target framework hint to use for all cross-targeting projects with multiple TFMs. This value must be a valid target framework for each individual cross-targeting project. Non-crosstargeting projects will ignore this hint and the value TFM configured in tye.yaml will override this hint.

  `-e|--environment <environment>`

    The environment to be used for deployment. Defaults to development if not specified.

  `--include-latest-tag`

    Push a docker image with 'latest' tag as well as versioned tag.

- `--force`

    Override validation and force push.

## Examples

- Push an application from the current directory:

    ```text
    tye push
    ```

- Push an application with interactive input:

    ```text
    tye push --interactive
    ```

- Push an application, increasing log verbosity to Debug.

    ```text
    tye build-push-deploy --verbosity Debug
    ```
