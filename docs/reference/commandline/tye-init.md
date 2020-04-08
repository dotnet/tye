# tye init

## Name

`tye init` - Scaffolds a tye.yaml file representing the application.

## Synopsis

```text
tye init [-?|-h|--help] [-f|--force] [<PATH>]
```

## Description

The `tye init` command scaffolds a `tye.yaml` file to allow customizing different aspects of `tye run` and `tye deploy`.

`tye init` will by default scaffold each project(s) in an application. For example, if there was a solution file (sln) that contained two csproj files, the `tye.yaml` contents would look like:

```yaml
name: application
services:
- name: project1
  project: project1.csproj
- name: project2
  project: project2/project2.csproj
```

See [the tye.yaml schema](/docs/reference/schema.md) to learn more about customizations that can be made to the `tye.yaml` file.

## Arguments

`PATH`

The path to either a file or directory to run `tye init` on. Can either be a yaml, sln, or project file.

## Options

- `-f|--force`

    If a `tye.yaml` file is already present, overwrites it with a newly scaffolded `tye.yaml`.

## Examples

- Scaffold a `tye.yaml` in the current directory:

    ```text
    tye init
    ```

- Scaffold a `tye.yaml` from a path to a directory:

    ```text
    tye init PATH_TO_DIRECTORY
    ```

- Scaffold a `tye.yaml` from a project file (*.csproj, *.fsproj):

    ```text
    tye init project.csproj
    ```

- Scaffold a `tye.yaml` from a solution file (sln):

    ```text
    tye init app.sln
    ```
