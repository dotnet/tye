# Getting Started

## Getting Started with Local Development

1. Install [.NET Core 3.1.](<http://dot.net>).
1. Install tye via the following command:

    ```text
    dotnet tool install -g tye --version 0.1.0-alpha.20161.4 --interactive --add-source https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal/nuget/v3/index.json
    ```

1. Verify the installation was complete by running:

    ```
    tye --version
    > 0.1.0-alpha.20161.4+d69009b73074973484b1602011dbb0c730f013bf
    ```

## Make a new application

1. Make a new folder called `microservice` and navigate to it:

    ```
    mkdir microservice
    cd microservice
    ```

1. Create a frontend project:

    ```
    dotnet new razor -n frontend
    ```

1. Run this new project with `tye` command line:

    ```
    tye run frontend
    ```

    With just a single application, tye will do two things: start the frontend application and run a dashboard. Navigate to <http://localhost:8000> to see the dashboard running.

    The dashboard should show the `frontend` application running. You should be able to view the application logs and you should be able to hit navigate to the application in your browser.

1. Create a backend API that the frontend will call inside of the `microservices/` folder.

    ```
    dotnet new webapi -n backend
    ```

1. Create a solution file and add both projects

    ```
    dotnet new sln
    dotnet sln add frontend
    dotnet sln add backend
    ```

    You should have a solution called `microservice.sln` that references the `frontend` and `backend` projects.

1. If you haven't already, stop the existing `tye run` command using `Ctrl + C`. Run the `tye` command line in the folder with the solution.

    ```
    tye run
    ```

    The dashboard should show both the `frontend` and `backend` services.

## Service Discovery and Communication

1. Now that we have 2 applications running, lets make them communicate. By default, `tye` enables service discovery by injecting environment variables with a specific naming convention.

    Add this method to the frontend project at the bottom of the Startup.cs class:
    ```C#
    private Uri GetUri(IConfiguration configuration, string name)
    {
        return new Uri($"http://{configuration[$"service:{name}:host"]}:{configuration[$"service:{name}:port"]}");
    }
    ```
    This method resolved the URL using the `tye` naming convention for services. For more information on this, see the [Service Definition](service_definition.md).

1. Add a file `WeatherForecast.cs` to the `frontend` project.
    ```C#
    using System;

    namespace frontend
    {
        public class WeatherForecast
        {
            public DateTime Date { get; set; }

            public int TemperatureC { get; set; }

            public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

            public string Summary { get; set; }
        }
    }
    ```

1. Add a file `WeatherClient.cs` to the `frontend` project with the following contents:
   ```C#
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    namespace frontend
    {
        public class WeatherClient
        {
            private readonly HttpClient client;

            public WeatherClient(HttpClient client)
            {
                this.client = client;
            }

            public async Task<WeatherForecast[]> GetWeatherAsync()
            {
                var responseMessage = await this.client.GetAsync("/weatherforecast");
                return await JsonSerializer.DeserializeAsync<WeatherForecast[]>(await responseMessage.Content.ReadAsStreamAsync());
            }
        }
    }
   ```

1. Now register this client in `Startup.cs` class in `ConfigureServices` of the `frontend` project:
   ```C#
   public void ConfigureServices(IServiceCollection services)
   {
       services.AddRazorPages();

       services.AddHttpClient<WeatherClient>(client =>
       {
            client.BaseAddress = GetUri(Configuration, "backend");
       });
   }
   ```
   This will wire up the `WeatherClient` to use the correct URL for the `backend` service.

1. Add a `Forecasts` property to the `Index` page model under `Pages\Index.cshtml.cs` in the `frontend` project.
    ```C#
    public WeatherForecast[] Forecasts { get; set; }
    ```

   Change the `OnGet` method to take the `WeatherClient` to call the `backend` service and store the result in the `Message` property:
   ```C#
   public async Task OnGet([FromServices]WeatherClient client)
   {
        Forecasts = await client.GetWeatherAsync();
   }
   ``` 

1. Change the `Index.cshtml` razor view to render the `Message` property in the razor page:
   ```html
   <div class="text-center">
       <h1 class="display-4">Welcome</h1>
       <p>Learn about <a href="https://docs.microsoft.com/aspnet/core">building Web apps with ASP.NET Core</a>.</p>
   </div>

   Weather Forecast:

    <table class="table">
        <thead>
            <tr>
                <th>Date</th>
                <th>Temp. (C)</th>
                <th>Temp. (F)</th>
                <th>Summary</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var forecast in @Model.Forecasts)
            {
                <tr>
                    <td>@forecast.Date.ToShortDateString()</td>
                    <td>@forecast.TemperatureC</td>
                    <td>@forecast.TemperatureF</td>
                    <td>@forecast.Summary</td>
                </tr>
            }
        </tbody>
    </table>
   ```

1. Run the project and the `frontend` service should be able to successfully call the `backend` service!

## Adding dependencies

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

## Getting Started with Deployment

1. Installing [docker](https://docs.docker.com/install/) on your operating system.

1. A container registry. Docker by default will create a container registry on [DockerHub](https://hub.docker.com/). You could also use [Azure Container Registry](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-acr) or another container registry of your choice.

1. A Kubernetes Cluster. You can try using a local Kubernetes cluster by enabling [Kubernetes in Docker Desktop](https://www.docker.com/blog/docker-windows-desktop-now-kubernetes/), however it does take up quite a bit of memory. You could also use [Azure Kubernetes Service](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-deploy-cluster) or another kubernetes provider of your choice.

## Deploying the application

Now that we have our application running locally with multiple containers, let's deploy the application. In this example, we will deploy to Kubernetes by using `tye deploy`.

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

1. Adding a container registry to `tye.yaml`

    Based on what container registry you configured, add the following line in the `tye.yaml` file:

    ```
    name: microservice
    registry: <registry_name>
    ```

    If you are using dockerhub, the registry_name will be in the format of 'example'. If you are using Azure Kubernetes Service (AKS), the registry_name will be in the format of example.azurecr.io.

1. Deploy to Kubernetes

    Next, deploy the rest of the application by running.

    ```
    tye deploy
    ```

    tye deploy will:

    - Create a docker image for each project in your application.
    - Push each docker image to your container registry.
    - Generate a Kubernetes Deployment and Service.
    - Apply the generated Deployment and Service to your current Kubernetes context.

1. Test it out!

    You should now see three pods running after deploying.

    ```
    kubectl get pods
    ```

    ```
    NAME                                             READY   STATUS    RESTARTS   AGE
    backend-ccfcd756f-xk2q9                          1/1     Running   0          85m
    frontend-84bbdf4f7d-6r5zp                        1/1     Running   0          85m
    redis-5f554bd8bd-rv26p                           1/1     Running   0          98m
    ```


## Going deep

Replicas
Setting up rabbitmq
