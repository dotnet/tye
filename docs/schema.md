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
  image: rabbitmq:3-management
  bindings:
    - port: 5672
      protocol: rabbitmq
```

### Terms

**application**: The *whole* application, (usually) includes multiple services. Maps conceptually to a `tye.yaml` or a `.sln`.

**service**: An individual project, container, or process. Part of an application.

**project**: A kind of service, specified by a buildable and runnable .NET project.

**container**: A kind of service that can be run locally using tye by launching a container using Docker.

**executable**: A kind of service that the can be run locally using an arbitrary command-line.

## Root Element Properties

### Root Element Example

```yaml
name: myapplication
registry: exampleuser
services:
```

### Root Properties

#### `name` (string) *required*

Configures the name of the application. This will appear in some Kubernetes labels right now, but not many other places.

#### `registry` (string)

Allows storing the name of the container registry in configuration. This is used when building and deploying images for two purposes:

- Determining how to tag the images
- Determining where to push the images

The registry could be a DockerHub username (`exampleuser`) or the hostname of a container registry (`example.azureci.io`).

If this is not specified in configuration, interactive deployments will prompt for it.

#### `services` (`Service[]`) *required*

Specifies the list of services. Applications must have at least one service.

## Service

`Service` elements appear in a list within the `services` root property.

Services must be one of the following kind:

- project
- container
- executable
- external

The kind will be inferred based on the properties that are set. Right now there is very little error checking or validation to help enforce this.

### Service Example

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
  image: rabbitmq:3-management
  bindings:
    - port: 5672
      protocol: rabbitmq
```

### Service Properties

#### `name` (string) *required*

The service name. Each service must have a name, and it must be a legal DNS name: (`a-z` + `_`).

#### `project` (string)

The relative path from `tye.yaml` to a `.csproj` or `.fsproj`. 

Including a `project` entry marks the service as a *project*:

- It will build and run locally using the .NET project during development. 
- It will be packaged and deployed during deployments.

#### `dockerImage` (string) (deprecated, use image instead)

The name and optional tag of an image that can be run using Docker. 

Including `image` marks the service as a *container*:

- It will pulled and run locally using Docker during development.
- It will not be deployed during deployment.


#### `image` (string)

The name and optional tag of an image that can be run using Docker. 

Including `image` marks the service as a *container*:

- It will pulled and run locally using Docker during development.
- It will not be deployed during deployment.

#### `executable` (string)

The path (or filename) of an executable to launch.

Including `executable` marks the service as an *executable*:

- It will run locally during development.
- It will not be deployed during deployment.

#### `external` (bool)

Including `external: true` marks the service as *external*:

- It will not run anything locally during development.
- It will not be deployed during deployment.

External services are useful to provide bindings without any run or deployment behavior.

#### `env` (`EnvironmentVariable[]`)

A list of environment variable mappings for the service. Does not apply when the service is external.

#### `args` (string)

Command-line arguments to use when launching the service. Does not apply when the service is external.

#### `replicas` (integer)

The number of replicas to create. Does not apply when the service is external.

#### `build` (bool)

Whether to build the project. Defaults to `true`. Only applies when the service is a project.

#### `workingDirectory` (string)

The working directory to use when launching. Only applies when the service is an executable.

#### `bindings` (`Binding[]`)

A list of bindings *exposed* by the service. Bindings represent protocols *provided* by a service.

## Environment Variables

`EnvironmentVariable` elements appear in a list inside the `env` property of a `Service`.

### Environment Variable Example

```yaml
name: myapplication
services:
- name: backend
  project: backend/backend.csproj

  # environment variables appear here
  env:
  - name: SOME_KEY
    value: SOME_VALUE
```

### Environment Variable Properties

#### `name` (string) *required*

The name of the environment variable.

#### `value` (string) *required*

The value of the environment variable.

## Bindings

`Binding` elements appear in a list inside the `bindings` property of a `Service`.

Bindings represent protocols *exposed* by a service. How bindings are specified can affect both:

- How a project is run.
- How [service discovery](/docs/service_discovery.md) is performed.

Bindings should either provide:

- A `connectionString`
- A `protocol` and `port`
- A `protocol` and `autoAssignPort: true`

### Binding Example

```yaml
name: myapplication
- name: rabbit
  image: rabbitmq:3-management

  # bindings appear here
  bindings:
    - port: 5672
      protocol: rabbitmq
```

### Binding Properties

#### `name` (string)

The name of the binding. Binding names are optional and should be omitted when a service contains a single binding. If a service provides two or more bindings, then they all must have names.

Names are part of the [service discovery](/docs/service_discovery.md) protocol.

#### `connectionString` (string)

The connection string of the binding. Connection strings should be used when connecting to the binding requires additional information besides a URL. [Service discovery](/docs/service_discovery.md) treats connection string as a single opaque value and will ignore other properties like `port`. 

As an example, connecting to a hosted redis using authentication requires a URL as well as username and password. Using a connection string is typical for databases or anything that requires authentication.

#### `protocol` (string)

Specifies the protocol used by the binding. The protocol is used in [service discovery](/docs/service_discovery.md) to construct a URL. It's safe to omit the `protocol` when it's not needed to connect to the service. As an example, connecting to redis without authentication only requires a hostname and port.

#### `host` (string)

Specifies the hostname used by the binding. The protocol is used in [service discovery](/docs/service_discovery.md) to construct a URL. It's safe to omit the `host` when localhost should be used for local development.

#### `port` (string)

Specifies the port used by the binding. The port is used in [service discovery](/docs/service_discovery.md) to construct a URL.

#### `internalPort` (string deprecated, use containerPort instead)

Specifies the port used by the binding when running in a docker container.

#### `containerPort` (string)

Specifies the port used by the binding when running in a docker container.

#### `autoAssignPort` (bool)

Specifies that the port should be assigned randomly. Defaults to `false`. This is currently only useful for projects - where the tye host will automatically infer bindings with `autoAssignPort: true`

## Volumes

`Volume` elements appear in a list inside the `volumes` property of a `Service`. Each volume specifies the local files or directories should be mapped into the docker container.

### Volumes Example

```yaml
name: myapplication
- name: nginx
  dockerImage: nginx

  # volumes appear here
  volumes:
    - source: config/nginx.conf
      target: /etc/nginx/conf.d/default.conf
```

### Volume Properties

#### `source` (string) *required*

The local path.

#### `target` (string) *required*

The destination path within the container.
