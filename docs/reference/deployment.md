# Deployment

*This document is a conceptual overview of how Tye behaves when using `tye build-push-deploy` for deployment. For reference docs  on the `tye build-push-deploy` command see the [command line doc](/docs/reference/commandline/tye-build-push-deploy.md).*

Tye simplifies many of the common concerns when deploying services, including creating docker files, docker images, Kubernetes manifests, and service discovery.

This document will describe how these features work, as well as the implications and behaviors of various optional settings.

## Executing `tye build-push-deploy`

When executing `tye build-push-deploy`, Tye goes through the following steps for each project:

- Configure defaults for docker
- Compute bindings
- Publish all projects
- Creates docker files and pushes images
- Validate secrets
- Generate and Apply Kubernetes manifests

These steps are sequentially executed for each project or service.

### Configure defaults for docker

Sets defaults for any projects that will create docker images. For example, for any ASP.NET Core projects, this step will set the container base image to `mcr.microsoft.com/dotnet/core/aspnet`. It will also select the image tag based on the .NET version specified (2.1, 3.1, 5, 6, etc.).

This is also where Tye requires a container registry to properly set the image name. If running `tye build-push-deploy` (no interactive), tye requires a registry to be defined in `tye.yaml`. If running interactively (`tye build-push-deploy -i`), tye will prompt the user for one if not specified in `tye.yaml`.

### Compute bindings

In order for services to communicate inside of a Kubernetes cluster, Tye will set environment variables appropriately services can communicate. This section will summarize some of the key information for deployment, but the best guide for the topic is the [service discovery documentation](/docs/reference/service_discovery.md).

Bindings for service discovery by default are `http` (not `https`) in Kubernetes. The default port will be port 80 for `http` if not specified.

Tye will set the environment variables for ASPNETCORE_URLS and PORT inside of a docker container. These values will be modified based on what is specified in the `tye.yaml` service bindings. ASPNETCORE_URLS by default will be `http://*` and PORT will be port 80.

For each service, tye will set environment variables for service discovery. The format for these environment variables are as follows

If there is a connection string for a binding:
```
CONNECTIONSTRINGS__{serviceName}={connectionString}
SERVICE__{serviceName}_PROTOCOL={protocol} (default to http)
SERVICE__{serviceName}_PORT={port} (default to 80)
SERVICE__{serviceName}_HOST={host} (default to serviceName as host)
```

If the target service is an external service, instead Tye will use Kubernetes secrets for service discovery rather than environment variables. See our [service discovery documentation](/docs/reference/service_discovery.md#How-it-works:-Deployed-applications) for in-depth information on why we use secrets instead of environment variables and how they are set.

### Publish all projects

Tye will publish all .NET projects in release mode via calling `dotnet publish`. If publish fails, tye will capture the output of dotnet publish and throw an exception.

### Create docker files and images

Tye will create docker files based on well known conventions for creating dotnet containers. The docker file output will look like:
```docker
FROM {baseImageName}:{baseImageTag}
WORKDIR /app
COPY . /app
ENTRYPOINT [\"dotnet\", \"{appName}.dll\"]
```

Tye will then execute `docker build`, where it will copy the output of dotnet publish to the container, creating and tagging the image.

Tye pushes the docker images created for a project. These are pushed to the registry based on the registry specified in `tye.yaml` what was specified in interactive deployment.

### Validate secrets

Earlier, during the compute bindings step, Tye determined which services required external configuration via secrets. For example, a connection string to redis will be required to run your services in Kubernetes. Tye will now validate that these secrets are present in Kubernetes.

Tye will look for an existing secret based on the service and binding names. Tye will call into Kubernetes to check the value of the secret. If the secret already exists then deployment will proceed.

If the secret does not exist, then Tye will prompt (in interactive mode) for the connection string or URI value, otherwise it will fail to deploy. Based on whether it's a connection string or URI, Tye will create a secret like one of the following.

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

If a secret needs to be applied, tye will call Kubernetes to apply it.

### Generate and Apply Kubernetes manifests

Tye will create the Kubernetes manifests from the projects. By default, each project will have an associated Kubernetes Deployment and Kubernetes Service in the generated manifest, like as follows.

```yaml
kind: Deployment
apiVersion: apps/v1
metadata:
  name: {serviceName}
  labels:
    app.kubernetes.io/name: '{serviceName}'
    app.kubernetes.io/part-of: '{appName}'
spec:
  replicas: 1
  selector:
    matchLabels:
      app.kubernetes.io/name: {serviceName}
  template:
    metadata:
      labels:
        app.kubernetes.io/name: '{serviceName}'
        app.kubernetes.io/part-of: '{appName}'
    spec:
      containers:
      - name: {serviceName}
        image: {imageName}:{imageTag}
        imagePullPolicy: Always
        env:
        - name: ASPNETCORE_URLS
          value: 'http://*'
        - name: PORT
          value: '80'
        - name: SERVICE__{otherService}__PROTOCOL
          value: 'http'
        - name: SERVICE__{otherService}__PORT
          value: '80'
        - name: SERVICE__{otherService}__HOST
          value: '{otherService}'
        # more environment variables here for service discovery.
        ports:
        - containerPort: 80
...
---
kind: Service
apiVersion: v1
metadata:
  name: {serviceName}
  labels:
    app.kubernetes.io/name: '{serviceName}'
    app.kubernetes.io/part-of: '{appName}'
spec:
  selector:
    app.kubernetes.io/name: {serviceName}
  type: ClusterIP
  ports:
  - name: http
    protocol: TCP
    port: 80
    targetPort: 80
```

Kubernetes labels follow a fairly straight forward convention:

- name for both Service and Deployment will be the name of the service or project.
- part-of will be the name of the application defined in tye.yaml (default to current directory name if undefined).

As shown, all environment variables for service discovery will be part of the Deployment manifest. If there is a secret, Tye will inject an environment variable as a secretKeyRef, like as follows.

```yaml
- name: CONNECTIONSTRINGS__POSTGRES
  valueFrom:
  secretKeyRef:
    key: connectionstring
    name: binding-production-postgres-secret
```
