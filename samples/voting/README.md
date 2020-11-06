# VotingSample
Voting sample app inspired by https://github.com/dockersamples/example-voting-app with a few different implementation choices.

## For running

The project should be immediately runnable by calling `tye run` from the directory.

## For deployment

A few things need to be configured before deploying to Kubernetes.

- [Create an Azure container registry using the Azure portal](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal) or you can use your public [docker hub account](https://hub.docker.com/)

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

>! NOTE: If you want, you can modify the password in `postgres` yaml.

- You need to install [NGINX Ingress Controller](https://kubernetes.github.io/ingress-nginx/) in your Kubernetes cluster. 
  You can also use a package manger like `helm` to [create an ingress controller](https://docs.microsoft.com/en-us/azure/aks/ingress-basic#create-an-ingress-controller) to expose your application outside of your Kubernetes cluster. 
  
  And then you need to expose those services by using ingress yaml. 
    
    ```
    kubectl apply -f ingress.yml
    ```

- After that, run `tye deploy --interactive --namespace default` to do all the deployment.

>! NOTE: Fill in the connection string for both `redis` and `postgres` when it's prompted.

Once the deployment is complete you should be able to browse the apps by the below Urls:

- vote : http://\<Cluster Public IP Or DNS\>/vote
- result: http://\<Cluster Public IP Or DNS\>/results




