# tye deploy

## Name

`tye deploy` - Deploys the application to Kubernetes.

## Synopsis

```text
tye deploy [-?|-h|--help] [-i|--interactive] [-v|--verbosity <Debug|Info|Quiet>] [-n|--namespace <n>] [-f|--framework <framework>] [--tags <tags>] [--force] [<PATH>]
```

## Description

The `tye deploy` command will deploy an application to Kubernetes. `tye deploy` by default will:

- Create a docker image for each project in your application.
- Push each docker image to your container registry.
- Generate a Kubernetes Deployment and Service for each project.
- Apply the generated Deployment and Service to your current Kubernetes context.

`tye deploy` chooses the Kubernetes namespace to operate in according to the following priority:

- The value of `--namespace` passed at the command line
- The value of `namespace` configured in `tye.yaml` (if present)
- The Kubernetes namespace for the current context

> :bulb: Use `kubectl config view --minify --output 'jsonpath={..namespace}'` to view the current namespace.

> :warning: The `tye deploy` command requires access to a remote container registry. Images will be tagged using the registry configured in `tye.yaml` (if present), or using a registry supplied interactively at the command line.

> :bulb: The `tye deploy` command uses Docker's credentials for pushing to the remote container registry. Make sure Docker is configured to push to your registry before running `tye deploy`.

> :bulb: The `tye deploy` command uses your local Kubernetes context to access the Kubernetes cluster. Make sure `kubectl` is configured to manage your cluster before running `tye deploy`.

## Arguments

`PATH`

The path to either a file or directory to execute `tye deploy` on. Can either be a yaml, sln, or project file, however it is recommend to have a tye.yaml file for `tye deploy`.

If a directory path is specified, `tye deploy` will default to using these files, in the following order:

- `tye.yaml`
- `*.sln`
- `*.csproj/*.fsproj`

## Options

- `-i|--interactive`

    Does an interactive deployment that will accept input for values that are required by default.

- `-v|--verbosity <Debug|Info|Quiet>`

    The verbosity of logs emitted by `tye deploy`. Defaults to Info.

- `-n|--namespace`

    Specifies the Kubernetes namespace for deployment. Overrides a namespace value set in `tye.yaml`.

- `-f|--framework <framework>`

    The target framework hint to use for all cross-targeting projects with multiple TFMs. This value must be a valid target framework for each individual cross-targeting project. Non-crosstargeting projects will ignore this hint and the value TFM configured in tye.yaml will override this hint.

- `--tags <tags>`

    Filter the group of running services by tag.

- `--force`

    Override validation and forces deployment.

## Examples

- Deploy an application from the current directory:

    ```text
    tye deploy
    ```

- Deploy an application with interactive input:

    ```text
    tye deploy --interactive
    ```

- Deploy an application, increasing log verbosity to Debug.

    ```text
    tye deploy --verbosity Debug
    ```
