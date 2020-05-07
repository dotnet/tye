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
namespace: examplenamespace
network: examplenetwork
ingress: ...
services: ...
```

### Root Properties

#### `name` (string)

Configures the name of the application. This will appear in some Kubernetes labels right now, but not many other places.

If the name name is not specified, then the lowercased directory name containing the `tye.yaml` file will be used as the default.

#### `registry` (string)

Allows storing the name of the container registry in configuration. This is used when building and deploying images for two purposes:

- Determining how to tag the images
- Determining where to push the images

The registry could be a DockerHub username (`exampleuser`) or the hostname of a container registry (`example.azureci.io`).

If this is not specified in configuration, interactive deployments will prompt for it.

#### `namespace` (string)

Allows configuring the Kubernetes namespace used by commands that operate on Kubernetes. If unconfigured, Tye will use the namespace of the current Kubernetes context.

The namespace specified in `tye.yaml` can be overridden at the command line.

> :bulb: Use `kubectl config view --minify --output 'jsonpath={..namespace}'` to view the current namespace.

#### `network` (string)

Allows configuring the Docker network used for `tye run`. 

If a network is configured, then all services running in containers will connect to the specified network. Otherwise a Docker network will be created with a generated name, and used to connect all containers.

#### `ingress` (`Ingress[]`)

Specifies the list of ingresses.

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
  # a reference to another tye.yaml
- name: poll
  include: ../poll/tye.yaml
```

### Service Properties

#### `name` (string) *required*

The service name. Each service must have a name, and it must be a legal DNS name: (`a-z` + `_`).

#### `project` (string)

The relative path from `tye.yaml` to a `.csproj` or `.fsproj`. 

Including a `project` entry marks the service as a *project*:

- It will build and run locally using the .NET project during development. 
- It will be packaged and deployed during deployments.

#### `image` (string)

The name and optional tag of an image that can be run using Docker.

Including `image` marks the service as a *container*:

- It will pulled and run locally using Docker during development.
- It will not be deployed during deployment.

#### `dockerFile` (string)

The Dockerfile to build from. Uses the name of the service as the name of the image. Only supported for `tye run` currently.

#### `dockerFileContext` (string)

Path to the Dockerfile Context to run docker build against. This is only used when `dockerFile` is specified as well.

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

#### `include` (string)

A path to another tye.yaml to be used by the application.

#### `repository` (string)

A reference to a repository that will be cloned and used by the application. By default, it is a string that would be passed after `git clone`.

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

## Build Properties

Configuration that can be specified when building a project. These will be passed in as MSBuild properties when building a project. It appears in the list `buildProperties` of a `Service`.

### Build Properties Example

```yaml
name: frontend-backend
services:
- name: backend
  project: backend/backend.csproj
  buildProperties:
  - name: Configuration
    value: Debug
- name: frontend
  project: frontend/frontend.csproj
  buildProperties:
  - name: Configuration
    value: Release
```

### Build Properties Definition

#### `name` (string) *required*

The name of the build property.

#### `value` (string) *required*

The value of the build property.

## Binding

`Binding` elements appear in a list inside the `bindings` property of a `Service`.

Bindings represent protocols *exposed* by a service. How bindings are specified can affect both:

- How a project is run.
- How [service discovery](/docs/reference/service_discovery.md) is performed.

Bindings should either provide:

- A `connectionString`
- A `protocol`
- A `port`

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

Names are part of the [service discovery](/docs/reference/service_discovery.md) protocol.

#### `connectionString` (string)

The connection string of the binding. Connection strings should be used when connecting to the binding requires additional information besides a URL. [Service discovery](/docs/reference/service_discovery.md) treats connection string as a single opaque value and will ignore other properties like `port`. 

As an example, connecting to a hosted redis using authentication requires a URL as well as username and password. Using a connection string is typical for databases or anything that requires authentication.

#### `protocol` (string)

Specifies the protocol used by the binding. The protocol is used in [service discovery](/docs/reference/service_discovery.md) to construct a URL. It's safe to omit the `protocol` when it's not needed to connect to the service. As an example, connecting to redis without authentication only requires a hostname and port.

#### `host` (string)

Specifies the hostname used by the binding. The protocol is used in [service discovery](/docs/reference/service_discovery.md) to construct a URL. It's safe to omit the `host` when localhost should be used for local development.

#### `port` (string)

Specifies the port used by the binding. The port is used in [service discovery](/docs/reference/service_discovery.md) to construct a URL.

#### `internalPort` (string deprecated, use containerPort instead)

Specifies the port used by the binding when running in a docker container.

#### `containerPort` (string)

Specifies the port used by the binding when running in a docker container.

#### `autoAssignPort` (bool deprecated, by default a port will be auto assigned if no connection string was specified)

Specifies that the port should be assigned randomly. Defaults to `false`. This is currently only useful for projects - where the tye host will automatically infer bindings with `autoAssignPort: true`

## Volumes

`Volume` elements appear in a list inside the `volumes` property of a `Service`. Each volume specifies the local files or directories should be mapped into the docker container.

### Volumes Example

```yaml
name: myapplication
- name: nginx
  image: nginx

  # volumes appear here
  volumes:
    - source: config/nginx.conf
      target: /etc/nginx/conf.d/default.conf
```

### Volume Properties

#### `source` (string)

The local path.

### `name` (string)

A named docker volume.

#### `target` (string) *required*

The destination path within the container.

## Ingress

`Ingress` elements appear in a list within the `ingress` root property.

Each ingress element represents an HTTP (L7) network proxy, capable of accepting public traffic from the internet:

- In development: a local proxy is used for testing purposes
- In deployed applications: a real load-balancer/proxy accepting traffic from outside the cluster is used

### Ingress Example

```yaml
ingress:
- name: example
  bindings:
  - port: 8080
  rules:
  - host: a.example.com
    service: app-a
  - host: b.example.com
    service: app-b 
```

This example configures a single ingress named `example` that routes traffic to `app-a` or `app-b` based on the `Host` header. The port `8080` will be used during local development.

### Ingress Properties

#### `name` (`string`) *required*

The name of the ingress.

#### `bindings` (`IngressBinding`)

The bindings of the ingress service when running locally. Ignored for deployed applications.

#### `rules` (`IngressRule`)

The rules used to route traffic for the ingress.

## IngressBinding

`IngressBinding` elements appear in an array within the `bindings` property of an `Ingress` element. The bindings of an ingress specify the ports of the ingress service when running locally. Bindings are ignored when deploying applications.

### IngressBinding Example

```yaml
ingress:
- name: example
  bindings:

  # An IngressBinding
  - port: 8080
    protocol: http

  rules:
  - host: a.example.com
    service: app-a
  - host: b.example.com
    service: app-b 
```

In this example the binding specifies that the ingress service should listen for `HTTP` on port 8080 when running locally for development.

### IngressBinding Properties

#### `name` (`string`)

The name of the binding.

#### `port` (`integer`)

The port of the binding.

#### `protocol` (`string`)

The protocol (`http` or `https`).

## IngressRule

`IngressRule` elements appear in an array within the `rules` property of the `Ingress` element. Rules configure the routing behavior of the ingress proxy.

Rules can configure routing based on path-prefix, or based on host (HTTP `Host` header) or both. 

- A rule without a `path` is considered to match all paths, which is the same as specifying `path: '/'`
- A rule without a `host` is considered to match all hosts.

### IngressRule Example

```yaml
ingress:
- name: example
  bindings:
  - port: 8080
    protocol: http
  rules:

  # Example IngressRules
  - host: a.example.com
    service: app-a
  - host: b.example.com
    path: /mypath
    service: app-b
```

### IngressRule Properties

#### `service` (`string`) *required*

The service to route traffic to. Must be the name of a service that is part of this application.

#### `path` (`string`)

The path-prefix to match. Matching is case-insensitive.

The `path` must begin with `/`:

- `/mypath` is value
- `mypath` is invalid

The `path` may end with a trailing slash - the behavior is the same whether or not a trailing slash appears.

The path `/mypath` or `/mypath/` will match:

- `/mypath`
- `/mypath/`
- `/MYPATH/something/else`

#### `host` (`string`)

The host to match. 