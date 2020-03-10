# Getting Started

This walkthrough assumes you are using .NET Core 3.1, but tye can be used with earlier versions of .NET Core.

## Pre-requisites

1. Install .NET Core from [.NET Downloads(<http://dot.net>) version 3.1.
1. Install tye via the following command:

    ```text
    dotnet tool install -g tye --version 0.1.0-alpha.20156.10 --add-source https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal/nuget/v3/index.json
    ```

1. Verify the installation was complete by running:

    ```
    tye --version
    > 0.1.0-alpha.20156.10+64482e9c2c9d6b13dadd79d600ca101ef34feb79
    ```

1. Installing [docker](https://docs.docker.com/install/) on your operating system.

1. A container registry. Docker by default will create a container registry on [DockerHub](https://hub.docker.com/). You could also use [Azure Container Registry](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-acr) or another container registry of your choice.

1. A Kubernetes Cluster. You can try using a local Kubernetes cluster by enabling [Kubernetes in Docker Desktop](https://www.docker.com/blog/docker-windows-desktop-now-kubernetes/), however it does take up quite a bit of memory. YOu could also use [Azure Kubernetes Service](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-deploy-cluster) or another kubernetes provider of your choice.

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
    tye run frontend --port 8001
    ```

    With just a single application, tye will do two things: start the frontend application and run a dashboard. Navigate to <http://localhost:8001> to see the dashboard running.

    The dashboard should show the `frontend` application running. You should be able to view the application logs and you should be able to hit navigate to the application in your browser.

1. Create a backend API that the frontend will call inside of the `microservices/` folder.

    ```
    dotnet new webapi -n backend
    ```

1. Change the ports to `5002` and `5003` on the `backend` project in `Properties/launchSettings.json`.
    ```JSON
    {
    ...
        "profiles": {
            ...
            "backend": {
                "commandName": "Project",
                "launchBrowser": true,
                "launchUrl": "weatherforecast",
                "applicationUrl": "https://localhost:5002;http://localhost:5003",
                "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                }
            }
        }
    }
    ```

    This avoids the port conflict between the frontend and the backend projects.

1. Create a solution file and add both projects

    ```
    dotnet new sln
    dotnet sln add frontend
    dotnet sln add backend
    ```

    You should have a solution called `microservice.sln` that references the `frontend` and `backend` projects.

1. If you haven't already, stop the existing `tye run` command using `Ctrl + C`. Run the `tye` command line in the folder with the solution.

    ```
    tye run --port 8001
    ```

    The dashboard should show both the `frontend` and `backend` services.

## Service Discovery and Communication

1. Now that we have 2 applications running, lets make them communicate. By default, **tye** enables service discovery by injecting environment variables with a specific naming convention.

    Add this method to the frontend project at the bottom of the Startup.cs class:
    ```C#
    private Uri GetUri(IConfiguration configuration, string name)
    {
        return new Uri($"http://{configuration[$"service:{name}:host"]}:{configuration[$"service:{name}:port"]}");
    }
    ```
    This method resolved the URL using the **tye** naming convention for services. For more information on this, see the [Service Definition](service_definition.md).

1. Add a file `WeatherClient.cs` to the `frontend` project with the following contents:
   ```C#
   using System.Net.Http;
   using System.Threading.Tasks;

   public class WeatherClient
   {
       private readonly HttpClient client;

       public WeatherClient(HttpClient client)
       {
           this.client = client;
       }

       public async Task<string> GetWeatherAsync()
       {
           return await this.client.GetStringAsync("/weatherforecast");
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

1. Add a `Message` property to the `Index` page model under `Pages\Index.cshtml.cs` in the `frontend` project.
   ```C#
   public string Message { get; set; }
   ```

   Change the `OnGet` method to take the `WeatherClient` to call the `backend` service and store the result in the `Message` property:
   ```C#
   public async Task OnGet([FromServices]WeatherClient client)
   {
       Message = await client.GetWeatherAsync();
   }
   ``` 

1. Change the `Index.cshtml` razor view to render the `Message` property in the razor page:
   ```html
   <div class="text-center">
       <h1 class="display-4">Welcome</h1>
       <p>Learn about <a href="https://docs.microsoft.com/aspnet/core">building Web apps with ASP.NET Core</a>.</p>
   </div>

   Weather Forecast:

   @Model.Message
   ```

1. Run the project and the `frontend` service should be able to successfully call the `backend` service!

## Adding dependencies

We just showed how **tye** makes it easier to communicate between 2 applications running locally but what happens if we want to use redis to store weather information?

### Docker

**Tye** can use `docker` to run images that run as part of your application. If you haven't already, make sure docker is installed on your operating system ([install docker](https://docs.docker.com/install/)) .

1. To create a **tye** manifest from the solution file.
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
           o.Configuration = $"{Configuration["service:redis:host"]}:{Configuration["service:redis:port"]}";
       });
   }
   ```
   The above configures redis to use the host and port for the `redis` service injected by the **tye** host.

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
   tye run --port 8001
   ```

   This should run the applications (including redis in the docker containers) and you should be able to view the logs for each of the services running. Navigate to the `frontend` project and verify that the data returned is the same after refreshing the page multiple times, and that new content is loaded after 15 seconds.

## Deploying the application

Now that we have our application running locally with multiple containers, let's deploy the application. In this example, we will deploy to Kubernetes.

1. Deploy to Kubernetes

    ```
    tye deploy -i
    ```

    Tye will prompt you for a container registry when required.

    tye deploy will:

    - Create a docker image.
    - Push the docker image to your repository.
    - Generate a Kubernetes Deployment and Service.
    - Apply the generated Deployment and Service to your current Kubernetes context.

1. Test it out!

You should now see three pods running after deploying.

```
kubectl get pods
```

```
NAME

```


## Going deep

Replicas
Setting up rabbitmq
