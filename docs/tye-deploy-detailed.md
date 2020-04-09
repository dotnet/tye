# Deployment

*This document is a conceptual overview of how Tye behaves when using `tye deploy` for deployment. For reference docs  

What does tye deploy do (sequence of steps)
 How do k8s manifests get generated
 How does app get containerized
 How do credentials work
 labels


Applies extensions,
Prompts for container registry,
Sets defaults for docker images.

Builds projects:
- Set environment variables for ASPNETCORE_URLS and PORT
- Compute other bindings for service discovery for a given service
- determine if needs secrets for bindings based on whether the other service is a project or not.

Publishes projects and put them into a container
- Allows for single phase or multi phase docker
- Runs dotnet publish in release mode
- Creates a temporary docker file based on whether there is a docker file present or not.
- builds a docker image from the docker file (docker build)
- smart taging

Push docker images:
- Runs docker push

Validates secrets:
- 
generates kubernetes manifests
Deploy yaml
