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

Tye uses a combination of environment variables and files on disk for specifying connection strings and URIs of services.

- Enviroment variables are used where possible because they are simple
- Files on disk are used for secrets in deployed applications because they are more secure
- Both of these are primitives that can be accessed from any programming langauge

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

## How it works: URIs

For URIs, the Tye infrastructure will generate a set of environment variables using a well-known pattern. These environment variables will through through the configuration system and by used by `GetServiceUri()`.

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

