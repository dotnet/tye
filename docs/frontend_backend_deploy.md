
# Getting Started with Deployment

This tutorial assumes that you have completed the [Frontend Backend Run Sample](frontend_backend_run.md)

Before we deploy, make sure you have the following installed on your machine.

1. Installing [docker](https://docs.docker.com/install/) based on your operating system.

1. A container registry. Docker by default will create a container registry on [DockerHub](https://hub.docker.com/). You could also use [Azure Container Registry](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-acr) or another container registry of your choice.

1. A Kubernetes Cluster. There are many different options here, including:
    - [Azure Kubernetes Service](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-deploy-cluster)
    - [Kubernetes in Docker Desktop](https://www.docker.com/blog/docker-windows-desktop-now-kubernetes/), however it does take up quite a bit of memory.
    - [Minikube](https://kubernetes.io/docs/tasks/tools/install-minikube/)
    - Another Kubernetes provider of your choice.

## Deploying the application

Now that we have our application running locally with multiple containers, let's deploy the application. In this example, we will deploy to Kubernetes by using `tye deploy`.

1. Adding a container registry to `tye.yaml`

    Based on what container registry you configured, add the following line in the `tye.yaml` file:

    ```yaml
    name: microservice
    registry: <registry_name>
    ```

    If you are using dockerhub, the registry_name will be in the format of 'example'. If you are using Azure Kubernetes Service (AKS), the registry_name will be in the format of example.azurecr.io.

1. Deploy to Kubernetes

    Next, deploy the rest of the application by running.

    ```text
    tye deploy
    ```

    `tye deploy` does many different things to deploy an application to Kubernetes. It will:
    - Create a docker image for each project in your application.
    - Push each docker image to your container registry.
    - Generate a Kubernetes Deployment and Service for each project.
    - Apply the generated Deployment and Service to your current Kubernetes context.

1. Test it out!

    You should now see two pods running after deploying.

    ```text
    kubectl get pods
    ```

    ```text
    NAME                                             READY   STATUS    RESTARTS   AGE
    backend-ccfcd756f-xk2q9                          1/1     Running   0          85m
    frontend-84bbdf4f7d-6r5zp                        1/1     Running   0          85m
    ```

    You can visit the frontend application by port forwarding to the frontend pod.

    ```text
    kubectl port-forward frontend-84bbdf4f7d-6r5zp 8000:80
    ```

    Now navigate to <http://localhost:8000> to view the frontend application working on Kubernetes.

## Next Steps

Now that you are able to deploy an application to Kubernetes, learn how to add a non-project dependency to tye with our [Redis](redis.md) tutorial.
