# Probes in Tye

Just like in a Kubernetes deployment, you can define `liveness` and `readiness` ports in Tye, as part of the service.   

## Definition

Tye will periodically make requests to an HTTP endpoint that is provided as part of the probe definition.  

If that HTTP endpoint returns a non-successful status code, Tye will fail the probe. (There is a threshold for how many times the HTTP request has to fail before the Tye fails the probe. the default is `3`).  

A failed probe has a different meaning for whether it's a liveness probe or a readiness probe.  

### Liveness

When the liveness probe fails for a certain replica, Tye restarts that replica.

### Readiness

When the readiness probe fails for a certain replica, Tye stops routing traffic to that replica, both from its service binding, and from any ingress that routes to that service.  

 ### Absence of a Probe Definition

 In the absence of both `liveness` and `readiness` probes, the replicas 

## Getting Started with Probes



## Sample Code

The sample code for this document can found [here](/../../samples/liveness-readiness)

This application has one service, with 2 replicas and an ingress that routes into it. 

