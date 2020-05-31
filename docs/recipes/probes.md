# Probes in Tye

Just like in a [Kubernetes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/) deployment, you can define `liveness` and `readiness` ports in Tye, as part of the service.   

Probes serve two main functions  

* Keeping traffic away from replicas which are not ready to receive it.
* Restarting replicas which are in a Bad/Unhealthy state.

## Life cycle of a Replica

Before we get to the definition of a probe and how it works, it's important to understand the life cycle of a replica.  

When using the local orchestrator (`tye run`), each replica in Tye can be in one of these three states  

* `Started` - A replica is in this state when it has just been started, and hasn't been probed yet.  
* `Healthy` - A replica is in this state when it passes the `liveness` probe, but not the `readiness` probe. 
* `Ready` - A replica is in this state when it passes both the `liveness` and `readiness` probes.  

(*Internally, there are more states that a replica can be in, but those are not relevant to this discussion*)

The orchestrator is responsible for switching between the different states based on the feedback from the probes, as described in later sections.  

When deploying the application to Kubernetes (via `tye deploy`), the life cycle is represented differently and is managed by Kubernetes. Click [here](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/) to read more about the life cycle of a Pod in Kubernetes.  

Throughout the rest of the document, we'll be referring to the life cycle of the replica as it is represented in the local orchestrator (i.e. `Started`, `Healthy`, `Ready`).

## Types of Probes  

There are two types of probes, both have a similar schema but serve a different purpose.  

### Liveness  

The `liveness` probe is used to let Tye know when it can restart a replica.  

When a replica is restarted due to a failed `liveness` probe, a new replica is created in its stead.

### Readiness  

The `readiness` probe is used to let Tye know when it's okay to route traffic to a replica.  

The `readiness` probe cannot kill/restart a replica, it can only do two things

* Promote `Healthy` replica to `Ready`, when the probe succeeds.
* Demote a `Ready` replica to `Healthy` when the probe fails.  

Tye only routes traffic (either via a service binding, or an ingress) to `Ready` replicas.  

## Life cycle of a Replica in the Absence of Probes

By now, the life cycle of a replica when both `liveness` and `readiness` probes are configured should be clear, but how does the life cycle of a replica look like when both probes or one of the probes is absent?  

* When neither `liveness` nor `readiness` probes are present, a replica gets promoted from `Started` to `Ready` automatically, upon creation.
* When only the `liveness` probe is present, a replica gets promoted from `Started` to `Healthy` only after it passes the `liveness` probe, but upon being promoted to `Healthy`, it's automatically promoted again, to `Ready`.  
* When only the `readiness` probe is present, a replica gets promoted from `Started` to `Healthy` automatically, upon creation, but gets promoted from `Healthy` to `Ready` only upon passing the `readiness` probe.  

## Running Locally with Liveness and Readiness Probes

*The section will refer to this [sample application](/samples/liveness-and-readiness/) which demonstrates basic usage.*  

To run the sample locally, clone this repository or download the source and navigate to the `/samples/liveness-and-readiness/` directory in your terminal.  

```
tye run
```

The sample has a single service with a `liveness` and a `readiness` probe, as shown in this snippet

```yaml
services:
  - name: simple-webapi
    project: webapi/webapi.csproj
    replicas: 2
    liveness:
      http:
        path: /lively
    readiness:
      http:
        path: /ready
```

The sample service uses the [Microsoft.Extensions.Diagnostics.HealthChecks](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics.HealthChecks) library to register two health checks, one for liveness (named `someLivenessCheck`) and another for readiness (named `someReadinessCheck`). both health checks are configured to respond positively by default.  

```C#
 services
    .AddHealthChecks()
    // this registers a "liveness" check. A service that fails a liveness check is considered to be unrecoverable and has to be restarted by the orchestrator (Tye/Kubernetes).
    // for example: you may consider failing this check if your service has encountered a fatal exception, or if you've detected a memory leak or a substantially long average response time
    .AddCheck("someLivenessCheck", new MyGenericCheck(_statusDictionary, "someLivenessCheck"), failureStatus: HealthStatus.Unhealthy, tags: new[] { "liveness" })
    // this registers a "readiness" check. A service that fails a readiness check is considered to be unable to serve traffic temporarily. The orchestrator doesn't restart a service that fails this check, but stops sending traffic to it until it responds to this check positively again.
    // for example: you may consider failing this check if your service is currently unable to connect to some external service such as your database, cache service, etc...
    .AddCheck("someReadinessCheck", new MyGenericCheck(_statusDictionary, "someReadinessCheck"), failureStatus: HealthStatus.Unhealthy, tags: new[] { "readiness" });
```

(Starting .NET 5, the HealthChecks library ships together with ASP.NET Core)

Since the service is configured to respond positively to all checks by default, after executing `tye run`, you should see these log lines  

```
[18:14:05 INF] Replica simple-webapi_a2d67bd9-4 is moving to an healthy state
[18:14:05 INF] Replica simple-webapi_a9c2e2f4-d is moving to an healthy state
[18:14:07 INF] Replica simple-webapi_a9c2e2f4-d is moving to a ready state
[18:14:07 INF] Replica simple-webapi_a2d67bd9-4 is moving to a ready state
```

As you can see, both replicas pass the `liveness` probe and get prompted to an `Healthy` state, and shortly after, both replica pass the `readiness` probe and get promoted to a `Ready` state.  

The sample service exposes an endpoint that allows you to modify the status of the health checks and thus affect the status that is returned from the `/lively` and `/ready` that in return affect the liveness and readiness probes of the service.  

For example, if you send an *HTTP GET* to `http://localhost:8080/set?someReadinessCheck=false&timeout=10` or enter that address in the browser,  
It will make `/ready` return *HTTP 500* for 10 seconds.   

Shortly after issuing that requests, you should see this log line in the terminal 

```
[18:14:18 INF] Replica simple-webapi_a2d67bd9-4 is moving to an healthy state
```

meaning that the replica got demoted from `Ready` to `Healthy`, due to failing the `readiness` probe.  

After *about* 10 seconds, you should see this log line in the terminal  

```
[18:14:26 INF] Replica simple-webapi_a2d67bd9-4 is moving to a ready state
```

meaning that the replica got demoted from `Healthy` to `Ready` again, due to passing the `readiness` probe.

(*The reason it's a bit less than 10 seconds, is because Tye doesn't fail the probe immediately. It waits for a certain number of consecutive failures, as described in the schema document.*)  

You can use the same method to make the `liveness` probe fail, and watch as Tye restarts the replica.  

Send an *HTTP GET* request to this endpoint `http://localhost:8080/set?someLivenessCheck=false`


And watch for this log line  

```
[18:25:08 INF] Killing replica simple-webapi_a9c2e2f4-d because it has failed the liveness probe
```

Shortly after, you should see this log lines  

```
[18:25:08 INF] Launching service simple-webapi_0e7fe12d-7
[18:25:09 INF] Replica simple-webapi_0e7fe12d-7 is moving to an healthy state
[18:25:11 INF] Replica simple-webapi_0e7fe12d-7 is moving to a ready state
```

Showing that Tye launches a new replica instead of the replica that it has killed.

## Deploying with Liveness and Readiness Probes

When you deploy an application with `liveness` and/or `readiness` probes to Kubernetes, these probes get translated to their [equivalent representation in Kubernetes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)  

After running

```
tye deploy --interactive
```

You should see the `simple-webapi` deployment in Kubernetes

```
NAME            READY   UP-TO-DATE   AVAILABLE   AGE
simple-webapi   2/2     2            2           90s
```

Run 

```
kubectl describe deploy simple-webapi
```

And you will notice that the deployment has `Liveness` and `Readiness` in its description  

```
Liveness:   http-get http://:80/lively delay=0s timeout=1s period=1s #success=1 #failure=3
Readiness:  http-get http://:80/ready delay=0s timeout=1s period=1s #success=1 #failure=3
```