# tye cleanup

## Name

`tye cleanup` - Removes a deployed application from Kubernetes.

## Synopsis 

```text
tye cleanup [-?|-h|--help] [-i|--interactive] [-v|--verbosity <Debug|Info|Quiet>] [--what-if] [<PATH>]
```

## Description

The `tye cleanup` command will delete a deployed application from Kubernetes. `tye cleanup` by default will:

- List all resources that are part of an application
- Print the list of resources (what-if)
- Offer a choice to delete each resource (interactive)
- Delete each resource (if applicable)

`tye cleanup` operates in the current Kubernetes namespace. Use `kubectl config view --minify --output 'jsonpath={..namespace}'` to view the current namespace.

Cleanup decides which resources to delete based on the `app.kubernetes.io/part-of=...` label. This label will be set to the application name for all resources created by Tye. `tye cleanup` does not rely on the list of services in `tye.yaml` or a solution file. 

## Arguments

`PATH`

The path to either a file or directory to execute `tye cleanup` on. Can either be a yaml, sln, or project file, however it is recommend to have a tye.yaml file for `tye cleanup`.

If a directory path is specified, `tye deploy` will default to using these files, in the following order:

- `tye.yaml`
- `*.sln`
- `*.csproj/*.fsproj`

## Options

- `-i|--interactive`

    Does an interactive cleanup that will prompt for deletion of each resource.

- `-v|--verbosity <Debug|Info|Quiet>`

    The verbosity of logs emitted by `tye cleanup`. Defaults to Info.

- `--what-if`

    Print each resource instead of deleting.

## Examples

- Deploy an application from the current directory:

    ```text
    tye deploy
    ```

- Deploy an application with interactive input:

    ```text
    tye run --interactive
    ```

- Deploy an application, increasing log verbosity to Debug.

    ```text
    tye deploy --verbosity Debug
    ```
