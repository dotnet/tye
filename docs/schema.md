# tye.yaml schema

`tye.yaml` is the configuration file format for `tye`. This document describes briefly the set of supported configuration settings and elements.

We also provide a JSON Schema which can be used by certain editors for completion and validation. See instructions [here](/src/schema/README.md).

### Example

```yaml
name: myapplication
services:
- name: backend
  project: backend/backend.csproj
  bindings:
  - port: 7000
- name: frontend
  project: frontend/frontend.csproj
  replicas: 2
  bindings:
  - port: 8000
- name: worker
  project: worker/worker.csproj
- name: rabbit
  dockerImage: rabbitmq:3-management
  bindings:
    - port: 5672
      protocol: rabbitmq
```

### Terms

**application**: The *whole* application, (usually) includes multiple services. Maps conceptually to a `tye.yaml` or a `.sln`.

**service**: An individual project, container, or process. Part of an appliction.

**project**: A kind of service, specified by a buildable and runnable .NET project.

**container**: A kind of service that can be run locally using tye by launching a container using Docker.

**executable**: A kind of service that the can be run locally using an arbitrary command-line.

## Root Element Properties

### Example

```yaml
name: myapplication
registry: exampleuser
services:
```

### Root Properties

- `name` (string)

Configures the name of the application. This will appear in some Kubernetes labels right now, but not many other places.

- `registry` (string)

Allows storing the name of the container registry in configuration. This is used when building and deploying images for two purposes:

- Determining how to tag the images
- Determining where to push the images

The registry could be a DockerHub username (`exampleuser`) or the hostname of a container registry (`example.azureci.io`).

If this is not specified in configuration, interactive deployments will prompt for it.

- `services` (`Service[]`)

Specifies the list of services. Applications must have at least one service.

## Services

Services appear in a list within the `services` root property.

### Example

```yaml
name: myapplication

# services appear under this property
services:
  # a project service
- name: backend
  project: backend/backend.csproj
  bindings:
  - port: 7000
  # a container service
- name: rabbit
  dockerImage: rabbitmq:3-management
  bindings:
    - port: 5672
      protocol: rabbitmq
```


