# Local Development

*This document is a conceptual overview of how Tye behaves when using `tye run` for local development. For reference docs on the `tye run` command see [here](/docs/reference/commandline/tye-run.md).*

Tye acts as a *local orchestrator* for development by building and launching multiple services, managing and monitoring services, and enabling network communication between services.

This document will describe how these features work, as well as the implications and behaviors of various optional settings.

## Executing `tye run`

When executing `tye run` for local development, Tye goes through the following steps:

- Load the application (`tye.yaml`)
- Build all projects 
- Pull all container images
- Assign ports and bindings
- Launch and monitor all services

## Loading the application

A Tye application is specified using `tye.yaml`. You can find more information about the Tye application format [here](/docs/reference/schema.md).

In the event that `tye run` is without a `tye.yaml` then the tool will populate the list of services using the provided solution file (multiple projects) or project file (single project).

Tye will ignore non-runnable projects when populating services based on a solution or project file, by excluding projects without a `launchSettings.json`.

---

Tye categorizes each service as either:

- A .NET Project
- A container image
- An executable
- (non-runnable) External


An example:

```yaml
name: service-kinds
services:

# A project service
- name: myproject
  project: myproject/myproject.csproj

# A container image service
- name: myimage
  image: redis

# An executable service
- name: myexecutable
  executable: notepad.exe

# An external service
- name: myproject
  external: true
```

---

After building the list of services, Tye will use MSBuild in-process to read additional details for any .NET projects added as services. Using MSBuild allows Tye to get information from projects in the same way as tools like IDEs and `dotnet build`.

The information gathered from projects includes details like the target framework, and binary output location, as well as settings that affect local development like `RunArguments`.

If the project uses ASP.NET Core, then Tye will assign some default bindings, an unnamed `http` binding and an `https` binding (appropriately named `https`).

For example if `frontend` and `backend` are ASP.NET Core projects, then the following:

```yaml
name: frontend-backend
services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
```

Will behave as-if the following had been written:

```yaml
name: frontend-backend
services:
- name: backend
  project: backend/backend.csproj
  bindings:
  - protocol: http
  - name: https
    protocol: https
- name: frontend
  project: frontend/frontend.csproj
  bindings:
  - protocol: http
  - name: https
    protocol: https
```

This binding inference saves a lot of typing in a very common case.

---

For other service types (container image, executable, external) no binding inference will take place, since it's not possible for Tye to introspect those service kinds. Add bindings manually for all of the network services that each service provides. See the [service discovery](/docs/reference/service_discovery.md) for more information and recommendations.

## Building projects

Tye will build all .NET projects (unless `build: false` has been specified) before running. Builds currently take place sequentially to avoid concurrency issues that can arise when executing MSBuild multiple times concurrently for the same project. 

## Pulling Container Images

Tye will use the Docker cli to pull any needed container images before launching any services. This is done as a separate step to avoid failures when the network is very slow or an image is very large. 

## Computing bindings

In order to help services communicate, Tye manages details like listening ports and hostnames. This listening information is specified to services using environment variables in development. This section will summarize some of the key information for local development, but the best guide for the topic is the [service discovery documentation](/docs/reference/service_discovery.md).

For each binding provided by a service, Tye assigns a unique network port (if the port is not specified in config). This port information will be used to generate the environment variables provided to services for service discovery. This port information is how other services will communicate with the service that defines the binding.

Consider an example:

```yaml
name: frontend-backend
services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
- name: redis
  image: redis
  bindings:
    - port: 6379
```

In this example, `backend` and `frontend` each have two bindings which will have auto-assigned ports. `redis` provides a single binding with a predetermined port.

The assigned ports might look like:

| **Service** | frontend (http) | frontend (https) | backend (http) | backend (https) | redis |
|-------------|-----------------|------------------|----------------|-----------------|-------|
|  **Port**   | 57540           | 57541            | 57542          | 57543           | 6379  |

Since the binding for `redis` supplies a hardcoded port (`6379`) then the value will always be `6379`. It's the user's responsibility to avoid conflicts when choosing a hardcoded port. Tye will not override a hardcoded port if there is a conflict, it will simply fail. ]

For all of the other bindings in this example, the ports are autogenerated based on what's available, and they will be unguessable. This is a basic scenario. Some other features of Tye change the details of how this works.

> :bulb: Avoid relying on hardcoded ports where possible. Hardcoding ports might be something you're used to, but it also ignores Tye's features that give you more flexibility. See the [service discovery](/docs/reference/service_discovery.md) docs for more information.

### Specifying a container port

In contrast to a .NET project or executable, a container image runs in a different namespace from where the user is working. Many containers also have a *preferred port* that they will listen on by default. Fortunately Docker provides features that allow mapping of an external port (on the user's system) to a different container port (inside the container).

From our previous example:

```yaml
name: frontend-backend
services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
- name: redis
  image: redis
  bindings:
    - port: 6379 # redis specifies a hardcoded port
```

To use this feature, specify `containerPort: 6379` instead of `port: 6379`. Tye will then assign a random port for the *external* port, and tell Docker to map it to 6379 inside the container.

Now our table from above might look like:

| **Service** | frontend (http) | frontend (https) | backend (http) | backend (https) | redis |
|-------------|-----------------|------------------|----------------|-----------------|-------|
|  **External Port**   | 57540           | 57541            | 57542          | 57543           | 57544  |
|  **Listening Port**   | 57540           | 57541            | 57542          | 57543           | 6379  |

So in this case the Redis service running inside the container is listening on port 6379, but the service is accessible outside the container on port 57544 (auto-assigned). The auto-assigned port 57544 for `redis` is used to populate the environment variables given to `frontend` and `backend` as part of service discovery.

## More TODO

- Replicas  
  - how replicas require/work with proxies

- Hostnames inside containers
  - how networking works inside containers vs not

- Running containers
  - volumes
  - secrets
  - logs

- Running .NET applications
  - env-vars passed to .net applications
  - logs
  - event-pipe/metrics

- Running .NET services in containers