# Adding redis to an application

This tutorial assumes that you have completed the [Frontend Backend Run Sample](frontend_backend_run.md) and [Frontend Backend Deploy Sample](frontend_backend_deploy.md).

We just showed how `tye` makes it easier to communicate between 2 applications running locally but what happens if we want to use redis to store weather information?

`Tye` can use `docker` to run images that run as part of your application. If you haven't already, make sure docker is installed on your operating system ([install docker](https://docs.docker.com/install/)) .


1. Change the `WeatherForecastController.Get()` method in the `backend` project to cache the weather information in redis using an `IDistributedCache`.

   Add the following `using`s to the top of the file:

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
               AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15)
           });
       }
       return weather;
   }
   ```

   This will store the weather data in Redis with an expiration time of 15 seconds.


2. Add a package reference to `Microsoft.Extensions.Caching.StackExchangeRedis` in the backend project:

   ```
   cd backend/
   dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
   ```

3. Modify `Startup.ConfigureServices` in the `backend` project to add the redis `IDistributedCache` implementation.
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

4. Modify `tye.yaml` to include redis as a dependency.

   > :bulb: You should have already created `tye.yaml` in a previous step near the end of the deployment tutorial.

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

    We've added 2 services to the `tye.yaml` file. The `redis` service itself and a `redis-cli` service that we will use to watch the data being sent to and retrieved from redis.

5. Run the `tye` command line in the solution root

   > :bulb: Make sure your command-line is in the `microservices/` directory. One of the previous steps had you change directories to edit a specific project.

   ```
   tye run
   ```

   Navigate to <http://localhost:8000> to see the dashboard running. Now you will see both `redis` and the `redis-cli` running listed in the dashboard.
   
   Navigate to the `frontend` application and verify that the data returned is the same after refreshing the page multiple times. New content will be loaded every 15 seconds, so if you wait that long and refresh again, you should see new data. You can also look at the `redis-cli` logs using the dashboard and see what data is being cached in redis.

## Deploying redis
   
1. Deploy redis to Kubernetes

    `tye deploy` will not deploy the redis configuration, so you need to deploy it first. Run:

    ```text
    kubectl apply -f https://raw.githubusercontent.com/dotnet/tye/master/docs/yaml/redis.yaml?token=AAK5D65XGABGEPUJ2MFJBM26O35M2
    ```

    This will create a deployment and service for redis. You can see that by running:

    ```text
    kubectl get deployments
    ```

    You will see redis deployed and running.

2. Add secrets to the application

    In order to access redis we need to add some code to the `backend` project to be able to read secrets from inside the container.

    First, add the `KeyPerFile` configuration provider package to the backend project using the command line.

    ```text
    cd backend
    dotnet add package Microsoft.Extensions.Configuration.KeyPerFile
    cd ..
    ```

    Next, add the following `using`s for the configuration provider near the top of `Program.cs`

    ```C#
    using System.IO;
    using Microsoft.Extensions.Configuration.KeyPerFile;
    ```

    Then, add the following method to the `Program` class:

    ```C#
    private static void AddTyeBindingSecrets(IConfigurationBuilder config)
    {
        if (Directory.Exists("/var/tye/bindings/"))
        {
            foreach (var directory in Directory.GetDirectories("/var/tye/bindings/"))
            {
                Console.WriteLine($"Adding config in '{directory}'.");
                config.AddKeyPerFile(directory, optional: true);
            }
        }
    }
    ```

    Then update `CreateHostBuilder` to call the new method:

    ```C#
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
            {
                AddTyeBindingSecrets(config);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    ```

3. Deploy to Kubernetes

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
        Created secret 'binding-production-redis-redis-secret'.
    ```

    > :question: `--interactive` is needed here to create the secret. This is a one-time configuration step. In a CI/CD scenario you would not want to have to specify connection strings over and over, deployment would rely on the existing configuration in the cluster.

    > :bulb: Tye uses kubernetes secrets to store connection information about dependencies like redis that might live outside the cluster. Tye will automatically generate mappings between service names, binding names, and secret names.

4. Test it out!

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