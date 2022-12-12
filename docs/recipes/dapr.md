# Using Tye with Dapr

You can use Tye to accelerate local development and deployments of [Dapr](https://dapr.io) applications. 

Documentation for using Dapr with .NET applications can be found here: https://github.com/dapr/dotnet-sdk.

This document will describe how to integrate a Dapr application with Tye. See Dapr's documentation on how to build applications that make use of what Dapr provides.

## Getting Started with Dapr

Getting started documentation for Dapr can be found [here](https://docs.dapr.io/getting-started/)

## Sample Code

There are two sample projects for the Dapr recipe [here](https://github.com/dotnet/tye/tree/main/samples/dapr).

They demonstrate

- Pub/Sub (The sample code associated with the instructions below.)
- Service Invocation

The pub-sub sample application has three services:

- A frontend application (`store`)
- A products backend service (`products`)
- An order fulfillment service (`orders`)

These services use a variety of Dapr's features:

- State Storage (`store`)
- Invoke (`store`, `products`)
- Pub/Sub (`store`, `orders`)

You can find the Dapr component files for the sample project [here](https://github.com/dotnet/tye/tree/main/samples/dapr/pub-sub/components).

## Running the sample locally

To run this sample, simply go to the `samples/dapr/pub-sub` directory and run the following command:

```sh
tye run
```

When the application runs, you should be able to see something like the following on the tye dashboard.

<img width="1497" alt="image" src="https://user-images.githubusercontent.com/1430011/77840703-2d977380-713f-11ea-9e76-560ce2b47cd3.png">

- Each .NET project has launched
- One `daprd` sidecar has launched per-project
- Redis has been launched as Dapr component

When using `tye run` in this way, Dapr discover components like redis for state storage and pubsub from the `components` directory next to the solution.

> :warning: You may encounter a port conflict for redis if you have already used `dapr --init` locally to start redis. This will likely be visible as a high number of restarts for the redis service in the dashboard. You can either use `dapr` to manage redis or `tye`, but not both. To work around this remove the `redis` service from `tye.yaml`.  

---

Without Tye in the picture, running these three services would require running a command like the following for each service:

```sh
dapr run --app-id store --app-port 5000 dotnet run --urls http://localhost:5000
```

Each application would need to be given a unique port to listen on, and launched in its own terminal window.

>:bulb: The current release of Dapr (0.5.1) [will not recognize](https://github.com/dapr/cli/issues/235) the `daprd` instances launched by Tye, and so commands like `dapr list` or `dapr publish` will not work when using Tye. This will be fixed in a future release.

---

Tye has built-in support that can make this more productive by:

- Launching everything at once
- Automatically managing ports

Tye's Dapr integration is activated in `tye.yaml` (seen below for this sample):

```yaml
name: dapr
extensions:
- name: dapr
services:
- name: orders
  project: orders/orders.csproj
- name: products
  project: products/products.csproj
- name: store
  project: store/store.csproj
- name: redis
  image: redis
  bindings:
    - port: 6379
``` 

All that's needed to enable Dapr integration for an application is:

```yaml
extensions:
- name: dapr
```

Additional [Dapr command arguments](https://docs.dapr.io/reference/arguments-annotations-overview/) 
can be specified for all services or for individual services.

```yaml
# Configure --app-max-concurrency for all services
extensions:
- name: dapr
  app-max-concurrency: 1
...
```

```yaml
# Configure --app-max-concurrency for the orders service
extensions:
- name: dapr
  services:
    orders:
      app-max-concurrency: 1
services:
- name: orders
  ...
```

The following Dapr arguments can be specified at the extension level (all
services) or the service level:

| Yaml Key              | Dapr Argument
| --------------------- | -------------
| app-max-concurrency   | app-max-concurrency
| app-protocol          | app-protocol
| app-ssl               | app-ssl
| components-path       | components-path
| config                | config
| enable-profiling      | enable-profiling
| http-max-request-size | dapr-http-max-request-size
| log-level             | log-level
| placement-port        | placement-host-address*

\*  When specifying `placement-port`, the placement host address will become `localhost:<placement-port>`.

The following Dapr arguments can be specified only at the service level:

| Yaml Key     | Dapr Argument
| ------------ | -------------
| app-id       | app-id
| grpc-port    | dapr-grpc-port
| http-port    | dapr-http-port
| metrics-port | metrics-port
| profile-port | profile-port

In addition, the key `enabled` can be specified (with `true` or `false`) to
enable or disable the related service.

## Deploying the sample to Kubernetes

**:warning: The current Dapr dotnet-sdk release has an issue where its default settings don't work when deployed with mTLS enabled. This will be resolved as part of the upcoming 0.6.0 release. For now you can work around this by disabling mTLS as part of Dapr installation.**

First, you will need a Kubernetes instance to deploy to. The [Basic Tutorial](/docs/tutorials/hello-tye/01_deploy.md) covers some options.

Secondly initialize Dapr for your cluster following the instructions [here](https://github.com/dapr/samples/tree/master/2.hello-kubernetes). Make sure to configure redis as both a state store and as pub-sub as described [here](https://github.com/dapr/docs/blob/master/howto/configure-redis/README.md#configuration).

You can verify that these steps have been performed correctly by running the following command:

```sh
dapr components --kubernetes
```

```txt
  NAME        TYPE          AGE  CREATED
  messagebus  pubsub.redis  13h  2020-03-28 22:26.45
  statestore  state.redis   13h  2020-03-28 22:26.40
```

---

Once these prerequisites have been taken care, you can deploy the application using Tye.

```sh
tye deploy --interactive
```

Using `--interactive` will allow you to enter your dockerhub username or container registry hostname when prompted.

You can verify that the application has been successfully deployed using `kubectl get pods`.

```text
> kubectl get pods 

NAME                        READY   STATUS        RESTARTS   AGE
orders-687db7fdbd-jhxfx     2/2     Running       0          23s
products-69f4c94684-lvt9x   2/2     Running       0          18s
store-7dc698f97d-v6hlb      2/2     Running       0          12s
```

You should see `2/2` ready for each pod. This means that the application and the Dapr sidecar have both started.

To access the application, port-forward to the `store` service. This will make the URL `http://localhost:5000` resolve to the instance running in the cluster for as long as the command is running.

```sh
kubectl port-forward svc/store 5000:80
```

>:bulb: If you need to see logs using `kubectl logs ...` you need to specify the container name when using Dapr, because there are multiple containers in the pod. Use `-c daprd` for the Dapr sidecar and `-c <projectname>` for the application (ex: `-c store`).
