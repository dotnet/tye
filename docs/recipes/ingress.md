# Ingress

For modern cloud runtime environments like Kubernetes, it's typical to keep the vast majority of services isolated from public internet traffic. Usually, there's a single (or few) point of entry for public traffic using a highly configurable proxy server.

This approach is referred to as *ingress*. The proxy server and surrounding management infrastructure are referred to as an *ingress controller* and the configurations of routing rules are referred to as *ingresses*. 

Tye provides an opinionated and simple way of configuring ingress for local development, as well as deploying to Kubernetes using the popular [NGINX Ingress Controller for Kubernetes](https://github.com/kubernetes/ingress-nginx).

> :bulb: Ingress features related to deployment are new as part of the Tye 0.2 release. The local run part of this tutorial will work with the latest nuget.org release. For deployment, follow the instructions [here](/docs/getting_started.md#working-with-ci-builds) to install a newer build.

## Running Locally with Ingress

*The section will refer to this [sample application](/samples/apps-with-ingress/) which demonstrates some basic usage*.

To run the sample locally, clone this repository or download the source and navigate to the `/samples/apps-with-ingress/` directory in your terminal.

```
tye run
```

Open the Tye dashboard in your browser at `http://localhost:8000`. 

You should see listings for three services on the dashboard:

- `app-a` (two replicas)
- `app-b` (two replicas)
- `ingress`

`app-a` and `app-b` are defined as normal .NET project services in the [tye.yaml](/samples/apps-with-ingress/tye.yaml). `ingress` is something special. 

Here's the relevant snippet from `tye.yaml`:

```yaml
ingress:
  - name: ingress
    bindings:
      - port: 8080
    rules:
      - path: /A
        service: app-a
      - path: /B
        service: app-b
      - host: a.example.com
        service: app-a
      - host: b.example.com
        service: app-b 
```

The block defines an ingress, as well as some routing rules. The ingress definition will create a service named `ingress` (as seen on the dashboard) as well as a listening port `8080`.

> :bulb: Usually it's not a good practice to hardcode ports in `tye.yaml` to avoid coupling - but you might find it convenient for an ingress. Since services won't usually need to initiate requests to the ingress, this doesn't introduce any bad hardcoding.

Each rule applies a routing rule mapping a path prefix or hostname to a service. It's possible to use both path and hostname in a single rule, in which case both conditions much be true for the rule to apply.

The ingress defines the processing priority of rules - in general, longer prefixes will be applied with higher priority, and things will work the way you expect. 

If no rules match the result with be an HTTP 404.

---

We can try sending some requests to the ingress to test out these routing rules.

Visiting the URI `http://localhost:8080/A` gives a response like:

```txt
Hello from Application A app-a_5fa11d8c-3
Path is: /
```

Any URI that matches `http://localhost:8080/A` or begins with `http://localhost:8080/A/` will be routed to an instance of `app-a`. Likewise `http://localhost:8080/B` or a URI beginning with `http://localhost:8080/B/` will be routed to `app-b`.

> :question: The output `app-a_5fa11d8c-3` in this example is a unique id assigned to the replica by the Tye Host using the environment variable `APP_INSTANCE`. Your replica instance ids will be different.

This behavior is defined by these rules:

```yaml
    rules:
      - path: /A
        service: app-a
      - path: /B
        service: app-b
```

You can also test different URIs that begin with one of these prefixes to see how the application responds.

For example: `http://localhost:8080/b/some/test/uri`

```
Hello from Application B app-b_d544d55e-a
Path is: /some/test/uri
```

A few important details:

- The ingress will load-balance requests among replicas. You will see different instance names in your responses (ex: `app-a_5fa11d8c-3` and `app-a_76dcbf72-3`).
- Path matching done by the ingress is case-insensitive.
- Path matching is prefix-based, and matches any suffix of the segments defined in `path`.
- The path prefix is *trimmed* before proxying the request. The original request might be `http://localhost:8080` but `app-a`

---

The other rules in `tye.yaml` use hostnames:

```yaml
    rules:
      - host: a.example.com
        service: app-a
      - host: b.example.com
        service: app-b 
```

This presents a problem for testing because you need to send a request to the server listening on `localhost:8080` on your machine, but need to have the `Host` header indicate either `a.example.com` or `b.example.com`. 

One approach would be to edit your `HOSTS` file to manually map these as DNS entries. Or, a simpler approach would be to use a test client that allows you to control the `Host` header.

Using `curl` in a terminal:

```
curl -H "Host: a.example.com" "http://localhost:8080/"

> Hello from Application A app-a_5fa11d8c-3
> Path is: /
```

Since there's no `path` specified for this rule, then nothing gets trimmed from the URI.

```
curl -H "Host: a.example.com" "http://localhost:8080/testpath"

> Hello from Application A app-a_76dcbf72-3
> Path is: /test/path
```

## Deploying with Ingress

>:bulb: Ingress features related to deployment are new as part of the Tye 0.2 release. The local run part of this tutorial will work with the latest nuget.org release. For deployment, follow the instructions [here](/docs/getting_started.md#working-with-ci-builds) to install a newer build.

Deploying an application with ingress to Kubernetes requires the deployment of an *ingress controller* to the cluster. Tye provides a guided installation workflow that will be described here. You may want to deploy the NGINX Ingress Controller for Kubernetes manually after reading [the instructions relevant to your cloud provider](https://kubernetes.github.io/ingress-nginx/deploy/).

These steps should still function if you have already deployed the NGINX Ingress Controller for Kubernetes. In that case you should not be prompted by Tye to install.

To deploy the application - do an interactive deployment from the terminal:

```
tye build-push-deploy --interactive
```

We're using an interactive deployment because Tye will need to prompt for:

- The container registry used to tag and push container images.
- Whether or not to deploy the ingress controller along with the application.

The output should look something like:

```txt
Loading Application Details...
Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub): test
Processing Service 'app-a'...
    Applying container defaults...
    Compiling Services...
    Publishing Project...
    Building Docker Image...
        Created Docker Image: 'test/app-a:0.2.0-dev'
    Pushing Docker Image...
        Pushed docker image: 'test/app-a:0.2.0-dev'
    Validating Secrets...
    Generating Manifests...
Processing Service 'app-b'...
    Applying container defaults...
    Compiling Services...
    Publishing Project...
    Building Docker Image...
        Created Docker Image: 'test/app-b:0.2.0-dev'
    Pushing Docker Image...
        Pushed docker image: 'test/app-b:0.2.0-dev'
    Validating Secrets...
    Generating Manifests...
Processing Ingress 'ingress'...
    Validating Ingress...
        Tye can deploy the ingress-nginx controller for you. This will be a basic deployment suitable for experimentation and development. Your production needs, or requirments may differ depending on your Kubernetes distribution. See: https://aka.ms/tye/ingress for documentation.
        Deploy ingress-nginx (y/n): y
        Waiting for ingress-nginx controller to start.
        Deployed ingress-nginx.
    Generating Manifests...
Deploying Application Manifests...

        Writing output to '/var/folders/p7/m9j35gn979x6w_jy66_xpf3h0000gn/T/tmpQiD5j3.tmp'.
        Deployed application 'apps-with-ingress'.
```

Performing a `kubectl get pods` should show two replicas of each service running:

```
NAME                     READY   STATUS    RESTARTS   AGE
app-a-84ff94fb89-tdv97   1/1     Running   0          3m5s
app-a-84ff94fb89-thkxq   1/1     Running   0          3m5s
app-b-78bc854fd-hs26c    1/1     Running   0          3m4s
app-b-78bc854fd-rq9fq    1/1     Running   0          3m4s
```

In addition to the typical pods, services, and deployments, you should also be able to query ingress with `kubectl get ingress`:

```
NAME      HOSTS                         ADDRESS         PORTS   AGE
ingress   a.example.com,b.example.com   51.143.54.113   80      4m16s
```

Depending on your Kubernetes distribution and the timing, you may not yet have a public address IP assigned. Keep trying for a few minutes (or use `kubectl get ingress -w`) until you see an address.

Once you have an address you can test it out with the same procedure as before. There's no need to `kubectl port-forward` to these services because they are now publicly accessible.

Substitute your IP address into the following command:

```
curl -H "Host: a.example.com" "http://<your ip address here>/"
Hello from Application A app-a-84ff94fb89-tdv97
Path is: /
```

All of the rules should function the same way now that the application is deployed. Feel free to run any of the test commands from the `tye run` section using the public IP and you should see a similar result.

---

To examine what was deployed for the ingress controller, run `kubectl get all -n ingress-nginx`:

```
NAME                                           READY   STATUS      RESTARTS   AGE
pod/ingress-nginx-admission-create-cjvb5       0/1     Completed   0          19m
pod/ingress-nginx-admission-patch-bfnqg        0/1     Completed   0          19m
pod/ingress-nginx-controller-c65667859-f8njz   1/1     Running     0          20m

NAME                                         TYPE           CLUSTER-IP     EXTERNAL-IP     PORT(S)                      AGE
service/ingress-nginx-controller             LoadBalancer   10.0.59.140    51.143.54.113   80:30950/TCP,443:31058/TCP   20m
service/ingress-nginx-controller-admission   ClusterIP      10.0.190.192   <none>          443/TCP                      20m

NAME                                       READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/ingress-nginx-controller   1/1     1            1           20m

NAME                                                 DESIRED   CURRENT   READY   AGE
replicaset.apps/ingress-nginx-controller-c65667859   1         1         1       20m

NAME                                       COMPLETIONS   DURATION   AGE
job.batch/ingress-nginx-admission-create   1/1           4s         20m
job.batch/ingress-nginx-admission-patch    1/1           5s         20m
```

This will list all of the resources of every type in the `ingress-nginx` namespace. It's not important to understand what all of these are.

The main component is the `ingress-nginx-controller` service which both acts as the controller and the proxy. You can see from the listing above that it has a public IP of type `LoadBalancer` assigned. 

> :warning: If you're using minikube you won't see output like the above. Instead, you'll probably see an error that the `ingress-nginx` namespace doesn't exist. This is because `ingress-nginx` in a simplified form is bundled as part of minikube. You can see the controller by running `kubectl get pods -A`.

---

When you're finished experimenting, run `tye undeploy` to delete the application from Kubernetes:

```
Loading Application Details...
Found 5 resource(s).
Deleting 'Service' 'app-a' ...
Deleting 'Service' 'app-b' ...
Deleting 'Deployment' 'app-a' ...
Deleting 'Deployment' 'app-b' ...
Deleting 'Ingress' 'ingress' ...
```

`tye undeploy` will not remove the ingress controller since ingress controllers are often shared by several applications. To remove the controller, run the following command:

```
kubectl delete -f https://aka.ms/tye/ingress/deploy
```

> :warning: If you're using minikube this command won't remove anything, and may error out. This is because `ingress-nginx` in a simplified form is bundled as part of minikube. You can disable the controller by running `minikube addons disable ingress`.
