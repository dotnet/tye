# Service Discovery

Service discovery is a general term that describes the process by which one service figures out the address of another service. Put another way...

> If I need to talk to the backend... how do I figure out the right URI? In production? In my staging environment? In my local development?

There are lots of possible different ways that service discovery could be done, with varying levels of complexity.

## Tye's philosophy for service discovery

Tye aims to provide a solution that:

- Works the same way in local development and cloud environments
- Is based on simple primitives
- Avoids the need for external infrastructure

Using Tye's service discovery features is *optional*. If you already have a scheme you like, or if you are using a programming model that solves service discovery in a different way then you can feel free to ignore it :+1:.

---

Tye uses environment variables for specifying connection strings and URIs of services.

Our philosophy is that automating something you could do yourself is better than doing *magic* or requiring external services.

It is our recommendation you avoid any hardcoding of URIs/addresses of other services in application code. Use service discovery via configuration so that deploying to different environments is easy.

## How to do service discovery in .NET Applications

The simple way to use Tye's service discovery is through the `Microsoft.Extensions.Configuration` system - available by default in ASP.NET Core or .NET Core Worker projects. In addition to this we provide the `Microsoft.Tye.Extensions.Configuration` package with some Tye-specific extensions layered on top of the configuration system.

---

To access URIs use the `GetServiceUri()` extension method and provide the service name.

```C#
// Get the URI of the 'backend' service and create an HttpClient.
var uri = Configuration.GetServiceUri("backend");
var httpClient = new HttpClient()
{
    BaseAddress = uri
};
```

If the service provides multiple bindings, provide the binding name as well.

```C#
// Get the URI of the 'backend' service and create an HttpClient.
var uri = Configuration.GetServiceUri(service: "backend", binding: "myBinding");
var httpClient = new HttpClient()
{
    BaseAddress = uri
};
```

URIs are available by default for all of your services and bindings. A URI will not be available through the service discovery system for bindings that provide a `connectionString` in config.

--- 

To access a connection string, use the `GetConnectionString()` method and provide the service name.

```C#
// Get the connection string of the 'postgres' service and open a database connection.
var connectionString = Configuration.GetConnectionString("postgres");
using (var connection = new NpgsqlConnection(connectionString))
{
    ...
}
```

To get the connection string for a named binding, pass the binding name as well.

```C#
// Get the connection string of the 'postgres' service and open a database connection.
var connectionString = Configuration.GetConnectionString(service: "postgres", binding: "myBinding");
using (var connection = new NpgsqlConnection(connectionString))
{
    ...
}
```

Connection strings will be available for bindings that use the `connectionString` property in configuration.

---

Specifying a connection string in `tye.yaml` will usually involve the use of templating to fill in values that are provided by Tye.

Example: Redis

```yaml
services:
- name: redis
  image: redis
  bindings:
  - port: 6379
    connectionString: ${host}:${port}
```

This fragment will launch `redis` when used with `tye run` on port `6379` (the typical listening port for Redis) **and** will provide a connection string to other services with the value of `localhost:6379`.

> :bulb: It's preferrable to use `${host}` over hardcoding the string `localhost` - for instance `localhost` will not work inside a container. You'll usually see `localhost` as the names of services, but Tye has features that will replace this with hostname values that work from containers.

--- 

Templating of connection strings can be used to avoid duplication.

Example: Postgres

```yaml
services:
- name: postgres
  image:  postgres
  env:
  - name: POSTGRES_PASSWORD
    value: "pass@word1"
  bindings:
  - port: 5432
    connectionString: Server=${host};Port=${port};User Id=postgres;Password=${env:POSTGRES_PASSWORD};
```

In this case a `postgres` container is being passed its password via the `POSTGRES_PASSWORD` value. The token `${env:POSTGRES_PASSWORD}` will be replaced with the value from `POSTGRES_PASSWORD` to avoid repetition.

Currently replacement of environment variables using this mechanism is limited to environment variables defined in `tye.yaml`.

This is a typical pattern for initializing a database for local development - initializing the password and passing it to the application in the same place.

> :bulb: Avoid generating connection strings, or hardcoding connection string parameters in application code. Tye allows you to configure connection strings differently between local development and deployed apps. The `connectionString` property in `tye.yaml` is not used in deployed applications, it's only for development.


## How it works: URIs in development

For URIs, the Tye infrastructure will generate a set of environment variables using a well-known pattern. These environment variables will be available through the configuration system and used by `GetServiceUri()`.

These are normal environment variables and can be read directly or through the configuration system.

The pattern for a default binding on the `backend` service:

|          | Environment Variable         | Configuration Key          |
|----------|------------------------------|----------------------------|
| Protocol | `SERVICE__BACKEND__PROTOCOL` | `service:backend:protocol` |
| Host     | `SERVICE__BACKEND__HOST`     | `service:backend:host`     |
| Port     | `SERVICE__BACKEND__PORT`     | `service:backend:port`     |


The pattern for a named binding called `myBinding` on the `backend` service:

|          | Environment Variable                    | Configuration Key                    |
|----------|-----------------------------------------|--------------------------------------|
| Protocol | `SERVICE__BACKEND__MYBINDING__PROTOCOL` | `service:backend:mybinding:protocol` |
| Host     | `SERVICE__BACKEND__MYBINDING__HOST`     | `service:backend:mybinding:host`     |
| Port     | `SERVICE__BACKEND__MYBINDING__PORT`     | `service:backend:mybinding:port`     |


> :bulb: That's a double-underscore (`__`) in the environment variables. The `Microsoft.Extensions.Configuration` system uses double-underscore as a separator because single underscores are already common in environment variables.

---

Here's a walkthrough of how this works in practice, using the following example application:

```yaml
name: frontend-backend
services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
```

These services are ASP.NET Core projects. The following set of steps takes place in development when doing `tye run`.

1. Since these are ASP.NET Core projects, Tye infers an `http` default binding, and an `https` named binding (called `https`) for each project. Since these bindings don't have any explicit configuration, ports will be automatically assigned by the host.
   
2. Tye will assign each binding an available port (avoiding conflicts).
   
3. Tye will generate a set of environment variables for each binding based on the assigned ports. The protocol of each binding was inferred (`http` or `https`) and the host will be `localhost` since this is local development.
   
4. Each service is given access to the environment variables that contain the bindings of the *other* services in the application. So `frontend` has access to the bindings of `backend` and vice-versa. These environment variables are passed when launching the process by the Tye host.
   
5. On startup, the environment variable configuration source reads all environment variables and translates them to the config key format (see table above).

6. When the application calls `GetServiceUri("backend")`, the method will read the `service:backend:protocol`, `service:backend:host`, `service:backend:port` variables and combine them into a URI.

## How it works: Connection Strings in development

When a `binding` element in `tye.yaml` sets the `connectionString` property, then Tye will not set the environment variables used for URIs (previous section). Tye will instead build a connection string and make that available.

Connection strings are typically used when a URI is not the right solution. For example, databases often have many configurable parameters beyond the hostname and port of the server.

Use the `GetConnectionString()` method to read connection strings.

These are normal environment variables and can be read directly or through the configuration system.

The pattern for a default binding on the `postgres` service:

|                   | Environment Variable         | Configuration Key          |
|-------------------|------------------------------|----------------------------|
| Connection String | `CONNECTIONSTRING__POSTGRES` | `connectionstrings:postgres` |


The pattern for a named binding called `myBinding` on the `postgres` service:

|                   | Environment Variable         | Configuration Key          |
|-------------------|------------------------------|----------------------------|
| Connection String | `CONNECTIONSTRING__POSTGRES__MYBINDING` | `connectionstrings:postgres:mybinding` |

> :bulb: That's a double-underscore (`__`) in the environment variables. The `Microsoft.Extensions.Configuration` system uses double-underscore as a separator because single underscores are already common in environment variables.

Here's a walkthrough of how this works in practice, using the following example application:

```yaml
services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
- name: postgres
  image:  postgres
  env:
  - name: POSTGRES_PASSWORD
    value: "pass@word1"
  bindings:
  - port: 5432
    connectionString: Server=${host};Port=${port};User Id=postgres;Password=${env:POSTGRES_PASSWORD};
```

We'll follow the example of the connection string for `postgres` is generated and made available to `frontend` and `backend`. The following set of steps takes place in development when doing `tye run`.

1. The `postgres` service has a single binding with a hardcoded port. If the port was unspecified then Tye will assign each binding an available port (avoiding conflicts).
   
2. Tye will substitute the values of `${host}` and `${port}` from the binding. Tye will substitue the value of `POSTGRES_PASSWORD` for `${env:POSTGRES_PASSWORD}`. The result is generate as the `CONNECTIONSTRINGS__POSTGRES` environment variable.
   
3. Each service is given access to the environment variables that contain the bindings of the *other* services in the application. So `frontend` and `backend` both have access to each-other's bindings as well as the environment variable `CONNECTIONSTRINGS__POSTGRES`. The Tye host will provide these environment variables when launching application processes.
   
4. On startup, the environment variable configuration source reads all environment variables and translates them to the config key format (see table above).

5. When the application calls `GetConnectionString("postgres")`, the method will read the `connectionstrings:postgres` key and return the value.

## How it works: Deployed applications

When deploying an application, Tye will deploy all of the containers built from your .NET projects. However, Tye is not able to deploy your application's dependencies - since Tye is orchestrating things, it needs to know what values (URIs and connection strings) to provide to application code.

---

When deploying your .NET projects, Tye will use the environment variable format described above to to set environment variables on your pods and containers.

To avoid hardcoding ephemeral details like pod names, Tye relies on Kubernetes Services. Each project gets its own Service, and the environment variables can refer to the hostname mapped to the service. 

This allows service discovery for URIs to work very simply in a deployed application. 

--- 

When an application contains a dependency (like Redis, or a Database), Tye will use Kubernetes Secret objects to store the connection string or URI.

Tye will look for an existing secret based on the service and binding names. If the secret already exists then deployment will proceed.

If the secret does not exist, then Tye will prompt (in interactive mode) for the connection string or URI value. Based on whether it's a connection string or URI, Tye will create a secret like one of the following.

Example secret for a URI:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: binding-production-rabbit-secret
type: Opaque
stringData:
  protocol: amqp
  host: rabbitmq
  port: 5672
```

Example secret for a connection string:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: binding-production-redis-secret
type: Opaque
stringData:
  connectionstring: <redacted>
```

Creating the secret is a one-time operation, and Tye will only prompt for it if it does not already exist. If desired you can use standard `kubectl` commands to update values or delete the secret and force it to be recreated.

To get these values into the application, Tye will use environment variables that reference the Kubernetes secrets described above and will use the environment-variable naming scheme described above.
