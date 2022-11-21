# Getting Started with Deployment

This tutorial assumes that you have completed the [first step (run locally)](00_run_locally.md)

> :bulb: `tye` will use your current credentials for pushing Docker images and accessing kubernetes clusters. If you have configured kubectl with a context already, that's what [`tye build-push-deploy`](/docs/reference/commandline/tye-build-push-deploy.md) is going to use!

Before we deploy, make sure you have the following ready...

1. Installing [docker](https://docs.docker.com/install/) based on your operating system.

2. A container registry. Docker by default will create a container registry on [DockerHub](https://hub.docker.com/). You could also use [Azure Container Registry](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-acr) or another container registry of your choice, like a [local registry](https://docs.docker.com/registry/deploying/#run-a-local-registry) for testing.

3. A Kubernetes Cluster. There are many different options here, including:
    - [Azure Kubernetes Service](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-deploy-cluster)
    - [Kubernetes in Docker Desktop](https://www.docker.com/blog/docker-windows-desktop-now-kubernetes/), however it does take up quite a bit of memory on your machine, so use with caution.
    - [Minikube](https://kubernetes.io/docs/tasks/tools/install-minikube/)
    - [K3s](https://k3s.io), a lightweight single-binary certified Kubernetes distribution from Rancher.
    - Another Kubernetes provider of your choice.

> :warning: If you choose a container registry provided by a cloud provider (other than Dockerhub), you will likely have to take some steps to configure your kubernetes cluster to allow access. Follow the instructions provided by your cloud provider.

## Deploying the application

Now that we have our application running locally, let's deploy the application. In this example, we will deploy to Kubernetes by using `tye build-push-deploy`.

1. Deploy to Kubernetes

    Deploy the application by running:

    ```text
    tye build-push-deploy --interactive
    ```
    > Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub):

    You will be prompted to enter your container registry. This is needed to tag images, and to push them to a location accessible by kubernetes.

    > :bulb: Under the hood `tye` uses `kubectl` to execute deployments. In cases where you don't have `kubectl` installed or it's current context is invalid `tye build-push-deploy` will fail with the following error: "Drats! 'build-push-deploy' failed: Cannot apply manifests because kubectl is not installed." 

    If you are using dockerhub, the registry name will be your dockerhub username. If you use a standalone container registry (for instance from your cloud provider), the registry name will look like a hostname, eg: `example.azurecr.io`.

    `tye build-push-deploy` does many different things to deploy an application to Kubernetes. It will:
    - Create a docker image for each project in your application.
    - Push each docker image to your container registry.
    - Generate a Kubernetes `Deployment` and `Service` for each project.
    - Apply the generated `Deployment` and `Service` to your current Kubernetes context.

2. Test it out!

    You should now see two pods running after deploying.

    ```text
    kubectl get pods
    ```

    ```text
    NAME                                             READY   STATUS    RESTARTS   AGE
    backend-ccfcd756f-xk2q9                          1/1     Running   0          85m
    frontend-84bbdf4f7d-6r5zp                        1/1     Running   0          85m
    ```

    You'll have two services in addition to the built-in `kubernetes` service.

    ```text
    kubectl get service
    ```

    ```text
    NAME         TYPE        CLUSTER-IP    EXTERNAL-IP   PORT(S)   AGE
    backend      ClusterIP   10.0.147.87   <none>        80/TCP    11s
    frontend     ClusterIP   10.0.20.168   <none>        80/TCP    14s
    kubernetes   ClusterIP   10.0.0.1      <none>        443/TCP   3d5h
    ```

    You can visit the frontend application by port forwarding to the frontend service.

    ```text
    kubectl port-forward svc/frontend 5000:80
    ```

    Now navigate to <http://localhost:5000> to view the frontend application running on Kubernetes. You should see the list of weather forecasts just like when you were running locally.

    > :bulb: Currently `tye` does not provide a way to expose pods/services to the public internet. We'll add features related to `Ingress` in future releases.

    > :warning: Currently `tye` does not automatically enable TLS within the cluster, and so communication takes place over HTTP instead of HTTPS. This is typical way to deploy services in kubernetes - we may look to enable TLS as an option or by default in the future.

## Exploring tye.yaml

Tye has a optional configuration file (`tye.yaml`) to allow customizing settings. If you want to use `tye build-push-deploy` as part of a CI/CD system, it's expected that you'll have a `tye.yaml`.

1. Scaffolding `tye.yaml`

    Run the `tye init` command in the `microservices` directory to generate a default `tye.yaml`

    ```text
    tye init
    ```

    The contents of `tye.yaml` should look like:

    ```yaml
    # tye application configuration file
    # read all about it at https://github.com/dotnet/tye
    #
    # when you've given us a try, we'd love to know what you think:
    #    <survey link>
    #
    name: microservice
    services:
    - name: frontend
      project: frontend/frontend.csproj
    - name: backend
      project: backend/backend.csproj
    ```

    The top level scope (like the `name` node) is where global settings are applied.

    `tye.yaml` lists all of the application's services under the `services` node. This is the place for per-service configuration.

    See [schema](/docs/reference/schema.md) for more details about `tye.yaml`.

    > :bulb: We provide a json-schema for `tye.yaml` and some editors support json-schema for completion and validation of yaml files. See [json-schema](/src/schema/README.md) for instructions.

2. Adding a container registry to `tye.yaml`

    Based on what container registry you configured, add the following line in the `tye.yaml` file:

    ```yaml
    registry: <registry_name>
    ```

    If you are using dockerhub, the registry_name will your dockerhub username. If you use a standalone container registry (for instance from your cloud provider), the registry_name will look like a hostname, eg: `example.azurecr.io`.

    Now it's possible to use `tye build-push-deploy` without `--interactive` since the registry is stored as part of configuration.

    > :question: This step may not make much sense if you're using `tye.yaml` to store a personal Dockerhub username. A more typical use case would be storing the name of a private registry for use in a CI/CD system.

## Undeploying the application

After deploying and playing around with the application, you may want to remove all resources associated from the Kubernetes cluster. You can remove resources by running:

```text
tye undeploy
```

This will remove all deployed resources. If you'd like to see what resources would be deleted, you can run:

```text
tye undeploy --what-if
```

## Next Steps

Now that you are able to deploy an application to Kubernetes, learn how to add a non-project dependency to tye with [the next step (add Redis)](02_add_redis.md).
