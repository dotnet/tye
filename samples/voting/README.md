# VotingSample

Voting sample app inspired by https://github.com/dockersamples/example-voting-app with a few different implementation choices.

## For running

The project should be immediately runnable by calling `tye run` from the directory.

## For deployment

A few things need to be configured before deploying to Kubernetes.

- [Create an Azure container registry using the Azure portal](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal) or you can use your public [docker hub account](https://hub.docker.com/)

- You can use managed service instances for both `redis` and `postgres` datastore. For e.g :

    - `redis` : *Azure Cache for Redis*
    - `postgres`: *Azure Database for PostgreSQL*

    Or you can use the following steps to deploy the respective datastores in the Kubernetes cluster itself.

- `Redis` can be deployed using the below command :

    ```
    kubectl apply -f https://raw.githubusercontent.com/dotnet/tye/master/docs/tutorials/hello-tye/redis.yaml
    ```
- `Postgresql` can be installed by the following command :

    ```
    kubectl apply -f https://raw.githubusercontent.com/dotnet/tye/master/docs/tutorials/hello-tye/postgres.yaml
    ```

- Once the deployment is done, you need to keep a note of the below connection strings :

    -  `redis:6379`
    -  `Server=postgres;Port=5432;User Id=postgres;Password=pass@word1;`

    >! NOTE: You can modify the password in `postgres` yaml.

- After that, run `tye deploy --interactive` to do the deployment.

    >! NOTE: You may need to pass the `--namespace` parameter if your Kubernetes namespace is not set by default. You can check that by using the command `kubectl config get-contexts`
    
- Fill in the value of container registry and connection strings for both `redis` and `postgres` when it's prompted.

- Once the deployment is complete you should be able to find the public IP address of the deployed application by using :

    ```
    kubectl get all -n ingress-nginx
    ```

    ![nginx ingress example](../../docs/recipes/images/nginx_ingress_action.png)

    Look for the `service/ingress-nginx-controller` with a type of `LoadBalancer`. The `EXTERNAL-IP` is the entry point for your application.
    For e.g :

    - vote : http://\<EXTERNAL-IP\>/vote
    - result: http://\<EXTERNAL-IP\>/results

    >! NOTE: Ingress controller may take a while to update the listed public IP address.