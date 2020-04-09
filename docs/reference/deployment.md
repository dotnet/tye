# Deployment

*This document is a conceptual overview of how Tye behaves when using `tye deploy` for deployment. For reference docs  on the `tye deploy` command see the [command line doc](/docs/reference/commandline/tye-deploy.md).*

Tye simplifies many of the common concerns when deploying services, including creating docker files, docker images, Kubernetes manifests, and service discovery.

This document will describe how these features work, as well as the implications and behaviors of various optional settings.

## Executing `tye deploy`

When executing `tye deploy`, Tye goes through the following steps for each project:

- Load the application (`tye.yaml`)
- Configure defaults for docker
- Publish all projects
- Creates docker files and images for each project
- Push docker images
- Validate secrets
- Generate Kubernetes manifests
- Apply Kubernetes manifests to current Kubernetes Cluster

These steps are sequentially executed for each project.

## Load the application

TODO pull from tye run doc.

## Configure defaults for docker

Sets defaults for any projects that will create docker images. For example, for any ASP.NET Core projects, this step will set the container base image to `mcr.microsoft.com/dotnet/core/aspnet`. It will also select the image tag based on the .NET version specified (2.1, 3.1, etc.).

This is also where Tye requires a container registry to properly set the image name. If running `tye deploy` (no interactive), tye requires a registry to be defined in `tye.yaml`. If running interactively (`tye deploy -i`), if there isn't a container registry specified in the `tye.yaml`, tye will prompt the user for one at this point.

## Compute bindings

In order for services to communicate inside of a Kubernetes cluster, Tye will set environment variables appropriately such that any service can communicate with any other service. Tye deploy has slightly different behavior than `tye run` here. This section will summarize some of the key information for deployment, but the best guide for the topic is the [service discovery documentation](/docs/reference/service_discovery.md).

Bindings for service discovery by default are `http` (not `https`) in Kubernetes. The default port will be port 80 for `http` if not specified.

Tye will set the environment variables for ASPNETCORE_URLS and PORT inside of a docker container. These values will be modified based on what is specified in the `tye.yaml` service bindings. ASPNETCORE_URLS by default will be `http://*` and PORT will be port 80.

For each service, tye will set environment variables for service discovery. Tye will set environment variables in the format of:

If there is a connection string for a binding:

CONNECTIONSTRINGS__{serviceName}={connectionString}
else:
SERVICE__{serviceName}_PROTOCOL={protocol} (default to http)
SERVICE__{serviceName}_PORT={port} (default to 80)
SERVICE__{serviceName}_HOST={host} (default to serviceName as host)

If the target service is an external service, instead tye will use Kubernetes secrets for service discovery rather than environment variables. See our [service discovery documentation](/docs/reference/service_discovery.md#How-it-works:-Deployed-applications) for in-depth information on why we use secrets instead of environment variables.

## Publish all projects

Tye will publish all .NET projects in release mode via calling `dotnet publish`. If publish fails, tye will capture the output of dotnet publish and throw an exception.

## Create docker files and images

- Allows for single phase or multi phase docker
- Creates a temporary docker file based on whether there is a docker file present or not.
- builds a docker image from the docker file (docker build)
- smart taging
TODO

## Push docker images

- Runs docker push
TODO

## Validate secrets

- How do credentials work
TODO

## Generate Kubernetes manifests

Mention how labels are created
TODO

## Apply Kubernetes manifests to current Kubernetes Cluster

TODO