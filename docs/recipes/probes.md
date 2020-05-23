# Probes in Tye

Just like in a [Kubernetes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/) deployment, you can define `liveness` and `readiness` ports in Tye, as part of the service.   

Probes serve two main functions  

* Keeping traffic away from replicas which are not ready to receive it.
* Restarting replicas which are in a Bad/Unhealthy state.

## Life cycle of a Replica

Before we get to the definition of a probe and how it works, it's important to understand the life cycle of a replica.  

Each replica in Tye can be in one of these three states  

* `Started` - A replica is in this state when it has just been started, and hasn't been probed yet.  
* `Healthy` - A replica is in this state when it passes the `liveness` probe, but not the `readiness` probe. 
* `Ready` - A replica is in this state when it passes both the `liveness` and `readiness` probes.  

(*Internally, there are more states that a replica can be in, but those are not relevant to this discussion*)

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

* When both `liveness` and `readiness` probes are absent, a replica gets promoted from `Started` to `Ready` automatically, upon creation.
* When only the `readiness` probe is absent, a replica gets promoted from `Started` to `Healthy` only after it passes the `liveness` probe, but upon being promoted to `Healthy`, it's automatically promoted again, to `Ready`.  
* When only the `liveness` probe is absent, a replica gets promoted from `Started` to `Healthy` automatically, upon creation, but gets promoted from `Healthy` to `Ready` only upon passing the `readiness` probe.  

## Running Locally with Liveness and Readiness Probes

*The section will refer to this [sample application](/samples/liveness-and-readiness/) which demonstrates basic usage.*  

To run the sample locally, clone this repository or download the source and navigate to the `/samples/liveness-and-readiness/` directory in your terminal.  

```
tye run
```

The sample has a single service with a `liveness` and a `readiness` probe, as shown in this snippet

```
services:
  - name: simple-webapi
    project: webapi/webapi.csproj
    replicas: 2
    liveness:
      http:
        path: /healthy
    readiness:
      http:
        path: /ready
```

The service is configured to respond successfully to both the `liveness` and `readiness` probes, so after you run Tye, you should see these log lines  

```
[18:14:05 INF] Replica simple-webapi_a2d67bd9-4 is moving to an healthy state
[18:14:05 INF] Replica simple-webapi_a9c2e2f4-d is moving to an healthy state
[18:14:07 INF] Replica simple-webapi_a9c2e2f4-d is moving to a ready state
[18:14:07 INF] Replica simple-webapi_a2d67bd9-4 is moving to a ready state
```

As you can see, both replicas pass the `liveness` probe and get prompted to an `Healthy` state, and shortly after, both replica pass the `readiness` probe and get promoted to a `Ready` state.  

The sample application exposes an endpoint that allows you modify the responses that the `/healthy` and `/ready` endpoints, in order to see the probes in action.  

For example, if you send an *HTTP POST* request to this endpoint `http://localhost:8080/set` with this body  

```
{
	"Ready": false,
	"Timeout": 10
}
```  

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

Send an *HTTP POST* request to this endpoint `http://localhost:8080/set` with this body
```
{
    "Healthy": false
}
```


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
Liveness:   http-get http://:80/healthy delay=0s timeout=1s period=1s #success=1 #failure=3
Readiness:  http-get http://:80/ready delay=0s timeout=1s period=1s #success=1 #failure=3
```