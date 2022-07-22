# Adding redis to an application

This tutorial assumes that you have completed the [first step (run locally)](00_run_locally.md) and [second step (deploy)](01_deploy.md).

We just showed how `tye` makes it easier to communicate between 2 applications running locally but what happens if we want to use redis to store weather information?

`Tye` can use `docker` to run images that run as part of your application. If you haven't already, make sure docker is installed on your operating system ([install docker](https://docs.docker.com/install/)) .


1. Change the `WeatherForecastController.Get()` method in the `backend` project to cache the weather information in redis using an `IDistributedCache`.

   Add the following `using`'s to the top of the file:

   ```C#
   using Microsoft.Extensions.Caching.Distributed;
   using System.Text.Json;
   ```

   And update `Get()`:

   ```C#
   [HttpGet]
   public async Task<string> Get([FromServices]IDistributedCache cache)
   {
       var weather = await cache.GetStringAsync("weather");

       if (weather == null)
       {
           var rng = new Random();
           var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
           {
               Date = DateTime.Now.AddDays(index),
               TemperatureC = rng.Next(-20, 55),
               Summary = Summaries[rng.Next(Summaries.Length)]
           })
           .ToArray();

           weather = JsonSerializer.Serialize(forecasts);

           await cache.SetStringAsync("weather", weather, new DistributedCacheEntryOptions
           {
               AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
           });
       }
       return weather;
   }
   ```

   This will store the weather data in Redis with an expiration time of 5 seconds.


2. Add a package reference to `Microsoft.Extensions.Caching.StackExchangeRedis` in the backend project:

   ```
   dotnet add backend/backend.csproj package Microsoft.Extensions.Caching.StackExchangeRedis
   ```

3. Modify `Startup.ConfigureServices` in the `backend` project to add the redis `IDistributedCache` implementation.

   ```C#
   public void ConfigureServices(IServiceCollection services)
   {
       services.AddControllers();

       services.AddStackExchangeRedisCache(o =>
       {
            o.Configuration = Configuration.GetConnectionString("redis");
       });
   }
   ```
   The above configures redis to the configuration string for the `redis` service injected by the `tye` host.

4. Modify `tye.yaml` to include redis as a dependency.

   > :bulb: You should have already created `tye.yaml` in a previous step near the end of the deployment tutorial.

   ```yaml
   name: microservice
   registry: <registry_name>
   services:
   - name: backend
     project: backend\backend.csproj
   - name: frontend
     project: frontend\frontend.csproj
   - name: redis
     image: redis
     bindings:
     - port: 6379
       connectionString: "${host}:${port}"
   - name: redis-cli
     image: redis
     args: "redis-cli -h redis MONITOR"
   ```

    We've added 2 services to the `tye.yaml` file. The `redis` service itself and a `redis-cli` service that we will use to watch the data being sent to and retrieved from redis.

    > :bulb: The `"${host}:${port}"` format in the `connectionString` property will substitute the values of the host and port number to produce a connection string that can be used with StackExchange.Redis.

5. Run the `tye run` command

   ```
   tye run
   ```

   Navigate to <http://localhost:8000> to see the dashboard running. Now you will see both `redis` and the `redis-cli` running listed in the dashboard.
   
   Navigate to the `frontend` application and verify that the data returned is the same after refreshing the page multiple times. New content will be loaded every 5 seconds, so if you wait that long and refresh again, you should see new data. You can also look at the `redis-cli` logs using the dashboard and see what data is being cached in redis.

## Deploying redis
   
1. Deploy redis to Kubernetes

    `tye deploy` will not deploy the redis configuration, so you need to deploy it first. Run:

    ```text
    kubectl apply -f https://raw.githubusercontent.com/dotnet/tye/main/docs/tutorials/hello-tye/redis.yaml
    ```

    This will create a deployment and service for redis. You can see that by running:

    ```text
    kubectl get deployments
    ```

    You will see redis deployed and running.

2. Deploy to Kubernetes

    Next, deploy the rest of the application by running:

    ```text
    tye deploy --interactive
    ```

    You'll be prompted for the connection string for redis. 

    ```text
    Validating Secrets...
        Enter the connection string to use for service 'redis':
    ```

    Enter the following to use instance that you just deployed:

    ```text
    redis:6379
    ```

    `tye deploy` will create kubernetes secret to store the connection string.

    ```text
    Validating Secrets...
        Enter the connection string to use for service 'redis': redis:6379
        Created secret 'binding-production-redis-secret'.
    ```

    > :question: `--interactive` is needed here to create the secret. This is a one-time configuration step. In a CI/CD scenario you would not want to have to specify connection strings over and over, deployment would rely on the existing configuration in the cluster.

    > :bulb: Tye uses kubernetes secrets to store connection information about dependencies like redis that might live outside the cluster. Tye will automatically generate mappings between service names, binding names, and secret names.

3. Test it out!

    You should now see three pods running after deploying.

    ```text
    kubectl get pods
    ```

    ```
    NAME                                             READY   STATUS    RESTARTS   AGE
    backend-ccfcd756f-xk2q9                          1/1     Running   0          85m
    frontend-84bbdf4f7d-6r5zp                        1/1     Running   0          85m
    redis-5f554bd8bd-rv26p                           1/1     Running   0          98m
    ```

    Just like last time, we'll need to port-forward to access the `frontend` from outside the cluster.

    ```text
    kubectl port-forward svc/frontend 5000:80
    ``` 

    Visit `http://localhost:5000` to see the `frontend` working in kubernetes.

4. Clean-up

    At this point, you may want to undeploy the application by running `tye undeploy`.
    
    Also clean up the Redis deployment and service by running
    
    ```text
    kubectl delete -f https://raw.githubusercontent.com/dotnet/tye/main/docs/tutorials/hello-tye/redis.yaml
    ```
    
