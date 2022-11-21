# tye build

## Name

`tye build` - Builds the application's containers.

## Synopsis

```text
tye build [-?|-h|--help] [-i|--interactive] [--tags <tags>] [-v|--verbosity <Debug|Info|Quiet>] [-f|--framework <framework>] [<PATH>]
```

## Description

The `tye build` command will build all of an application's project services into containers without deploying or pushing the containers remotely.

This command is useful for testing that all projects successfully build.

## Arguments

`PATH`

The path to either a file or directory to execute `tye build` on.

If a directory path is specified, `tye build` will default to using these files, in the following order:

- `tye.yaml`
- `*.sln`
- `*.csproj/*.fsproj`

## Options

- `-i|--interactive`

    Interactive mode.

- `--tags <tags>`

    Filter the group of running services by tag.

- `-v|--verbosity <Debug|Info|Quiet>`

    The verbosity of logs emitted by `tye build`. Defaults to Info.

- `-f|--framework <framework>`

    The target framework hint to use for all cross-targeting projects with multiple TFMs. This value must be a valid target framework for each individual cross-targeting project. Non-crosstargeting projects will ignore this hint and the value TFM configured in tye.yaml will override this hint.

  `-e|--environment <environment>`

    The environment to be used for deployment. Defaults to development if not specified.

## Examples

- Build an application from the current directory:

    ```text
    tye build
    ```

- Build an application, increasing log verbosity to Debug.

    ```text
    tye build --verbosity Debug
    ```
