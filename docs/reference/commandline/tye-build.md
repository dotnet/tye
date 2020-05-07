# tye build

## Name

`tye build` - Builds the application's containers.

## Synopsis 

```text
tye build [-?|-h|--help] [-v|--verbosity <Debug|Info|Quiet>] [<PATH>]
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

- `-v|--verbosity <Debug|Info|Quiet>`

    The verbosity of logs emitted by `tye build`. Defaults to Info.


## Examples

- Build an application from the current directory:

    ```text
    tye build
    ```

- Build an application, increasing log verbosity to Debug.

    ```text
    tye build --verbosity Debug
    ```
