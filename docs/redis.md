# Adding redis to an application

This tutorial assumes that you have completed the [Frontend Backend sample](frontend_backend_run.md).

We just showed how `tye` makes it easier to communicate between 2 applications running locally but what happens if we want to use redis to store weather information?

`Tye` can use `docker` to run images that run as part of your application. If you haven't already, make sure docker is installed on your operating system ([install docker](https://docs.docker.com/install/)) .

1. To create a `tye` manifest from the solution file.
   ```
   tye init microservice.sln
   ```
   This will create a manifest called `tye.yaml` with the following contents:
   ```yaml
    name: microservice
    services:
    - name: frontend
        project: frontend\frontend.csproj
    - name: backend
        project: backend\backend.csproj
   ```

   This will be the source of truth for `tye` execution from now on. To see a full schema of file, see the reference in the [schema reference](schema.md).

1. Change the `WeatherForecastController.Get` method in the `backend` project to cache the weather information in redis using an `IDistributedCache`.
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
               AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15)
           });
       }
       return weather;
   }
   ```

   This will store the weather data in Redis with an expiration time of 15 seconds.

1. Add a package reference to `Microsoft.Extensions.Caching.StackExchangeRedis` in the backend project:

   ```
   cd backend/
   dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
   ```

1. Modify `Startup.ConfigureServices` in the `backend` project to add the redis `IDistributedCache` implementation.
   ```C#
   public void ConfigureServices(IServiceCollection services)
   {
       services.AddControllers();

       services.AddStackExchangeRedisCache(o =>
       {
            var connectionString = Configuration["connectionstring:redis"];
            if (connectionString != null)
            {
                o.Configuration = connectionString;
            }
            else
            {
                o.Configuration = $"{Configuration["service:redis:host"]}:{Configuration["service:redis:port"]}";
            }
        });
   }
   ```
   The above configures redis to use the host and port for the `redis` service injected by the `tye` host.

1. Modify `tye.yaml` to include redis as a dependency.

   ```yaml
    name: microservice
    services:
    - name: backend
        project: backend\backend.csproj
    - name: frontend
        project: frontend\frontend.csproj
    - name: redis
        dockerImage: redis
        bindings:
        - port: 6379
    - name: redis-cli
        dockerImage: redis
        args: "redis-cli -h host.docker.internal MONITOR"
   ```

    We've added 2 services to the `tye.yaml` file. The redis service itself and a redis-cli service that we will use to watch the data being sent to and retrieved from redis.

1. Run the `tye` command line in the solution root

   ```
   tye run
   ```

   Navigate to <http://localhost:8000> to see the dashboard running. Now you will see both `redis` and the `redis-cli` running. Navigate to the `frontend` application and verify that the data returned is the same after refreshing the page multiple times. New content will be loaded every 15 seconds, so if you wait that long and refresh again, you should see new data. You can also look at the redis-cli logs and see what data is being cached in redis.

## Deploying redis
   
1. Deploy redis to Kubernetes

    `tye deploy` will not deploy the redis configuration, so you need to deploy it first. Run:
    ```
    kubectl apply -f https://raw.githubusercontent.com/dotnet/tye/d79f790ba13791c1964ed03c31da0cd12b101f39/docs/yaml/redis.yaml?token=AB7K4FLEULBCQQU6NLXZEDC6OPIU4
    ```

    This will create a deployment and service for redis. You can see that by running:
    ```
    kubectl get deployments
    ```

    You will see redis deployed and running.

1. Deploy to Kubernetes

    Next, deploy the rest of the application by running.

    ```
    tye deploy
    ```

1. Test it out!

    You should now see three pods running after deploying.

    ```
    kubectl get pods
    ```

    ```
    NAME                                             READY   STATUS    RESTARTS   AGE
    backend-ccfcd756f-xk2q9                          1/1     Running   0          85m
    frontend-84bbdf4f7d-6r5zp                        1/1     Running   0          85m
    ```
