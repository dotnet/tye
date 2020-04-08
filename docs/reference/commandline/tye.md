# tye command

## Name

`tye` - The base tye command.

## Synopsis

To get more information about the available commands:

```text

tye [-?|-h|--help] [--version]
```

## Description

The `tye` command provides commands for working with a multi-project application.

For example, [`tye run`](tye-run.md) runs an application. Each command defines its own options and arguments. All commands support the `--help` option for printing out brief documentation about how to use the command.

## Options

- **`--version`**

  Prints out the version of tye in use.

## tye commands

| Command                                       | Function                                                            |
| --------------------------------------------- | ------------------------------------------------------------------- |
| [tye init](tye-init.md) | Scaffolds a tye.yaml file representing the application.                          |
| [tye run](tye-run.md)               | Runs an application.                                     |
| [tye deploy](tye-deploy.md)               | Deploys an application.                                             |
