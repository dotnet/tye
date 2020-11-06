# tye undeploy

## Name

`tye undeploy` - Removes a deployed application from Kubernetes.

## Synopsis

```text
tye undeploy [-?|-h|--help] [-n|--namespace <n>] [-i|--interactive] [-v|--verbosity <Debug|Info|Quiet>] [--tags <tags>] [--what-if] [<PATH>]
```

## Description

The `tye undeploy` command will delete a deployed application from Kubernetes. `tye undeploy` by default will:

- List all resources that are part of an application
- Print the list of resources (what-if)
- Offer a choice to delete each resource (interactive)
- Delete each resource (if applicable)

`tye undeploy` chooses the Kubernetes namespace to operate in according to the following priority:

- The value of `--namespace` passed at the command line
- The value of `namespace` configured in `tye.yaml` (if present)
- The Kubernetes namespace for the current context

> :bulb: Use `kubectl config view --minify --output 'jsonpath={..namespace}'` to view the current namespace.

Undeploy decides which resources to delete based on the `app.kubernetes.io/part-of=...` label. This label will be set to the application name for all resources created by Tye. `tye undeploy` does not rely on the list of services in `tye.yaml` or a solution file.

> :bulb: The `tye undeploy` command uses your local Kubernetes context to access the Kubernetes cluster. Make sure `kubectl` is configured to manage your cluster before running `tye undeploy`.

## Arguments

`PATH`

The path to either a file or directory to execute `tye undeploy` on. Can either be a yaml, sln, or project file, however it is recommend to have a tye.yaml file for `tye undeploy`.

If a directory path is specified, `tye undeploy` will default to using these files, in the following order:

- `tye.yaml`
- `*.sln`
- `*.csproj/*.fsproj`

## Options

- `-n|--namespace`

    Specifies the Kubernetes namespace for deployment. Overrides a namespace value set in `tye.yaml`.

- `-i|--interactive`

    Does an interactive undeploy that will prompt for deletion of each resource.

- `-v|--verbosity <Debug|Info|Quiet>`

    The verbosity of logs emitted by `tye undeploy`. Defaults to Info.

- `--tags <tags>`

    Filter the group of running services by tag.

- `--what-if`

    Print each resource instead of deleting.

## Examples

- Delete a deployed application from the current directory:

    ```text
    tye undeploy
    ```

- Delete a deployed application with interactive input:

    ```text
    tye undeploy --interactive
    ```

- Display the resources that would be deleted by an undeploy operation:

    ```text
    tye undeploy --what-if
    ```
