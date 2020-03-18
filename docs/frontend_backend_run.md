# Frontend Backend sample with tye run

This tutorial will demonstrate how to use [`tye run`](commandline/tye-run.md) to run a multi-project application. If you haven't so already, follow the [Getting Started Instructions](getting_started.md) to install tye.

## Running a single application with tye run

1. Make a new folder called `microservice` and navigate to it:

    ```text
    mkdir microservice
    cd microservice
    ```

1. Create a frontend project:

    ```text
    dotnet new razor -n frontend
    ```

1. Run this new project with `tye` command line:

    ```text
    tye run frontend
    ```

    With just a single application, tye will do two things: start the frontend application and run a dashboard. Navigate to <http://localhost:8000> to see the dashboard running.

    The dashboard should show the `frontend` application running.

    - The `Logs` column has a link to view the streaming logs for the service.
    - the `Bindings` column has links to the listening URLs of the service.
    
    Navigate to the `frontend` service using one of the links on the dashboard.

    The dashboard will use port 8000 if possible. Services written using ASP.NET Core will have their listening ports assigned randomly if not explicitly configured.

## Running multiple applications with tye run

1. Create a backend API that the frontend will call inside of the `microservices/` folder.

    ```text
    dotnet new webapi -n backend
    ```

1. Create a solution file and add both projects

    ```text
    dotnet new sln
    dotnet sln add frontend backend
    ```

    You should have a solution called `microservice.sln` that references the `frontend` and `backend` projects.

2. If you haven't already, stop the existing `tye run` command using `Ctrl + C`. Run the `tye` command line in the folder with the solution.

    ```text
    tye run
    ```

    The dashboard should show both the `frontend` and `backend` services. You can navigate to both of them through either the dashboard of the url outputted by `tye run`.

    > :warning: The `backend` service in this example was created using the `webapi` project template and will return an HTTP 404 for its root URL.

## Getting the frontend to communicate with the backend

Now that we have two applications running, let's make them communicate. By default, `tye` enables service discovery by injecting environment variables with a specific naming convention.

1. Open the solution in your editor of choice.

1. Add a `GetUri()` method to the frontend project at the bottom of the Startup.cs class:

    ```C#
    private Uri GetUri(IConfiguration configuration, string name)
    {
        return new Uri($"http://{configuration[$"service:{name}:host"]}:{configuration[$"service:{name}:port"]}");
    }
    ```

    This method resolved the URL using the `tye` naming convention for services. For more information on, see [service discovery](service_discovery.md).

2. Add a file `WeatherForecast.cs` to the `frontend` project.

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

    This will match the backend `WeatherForecast.cs`.

3. Add a file `WeatherClient.cs` to the `frontend` project with the following contents:

   ```C#
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    namespace frontend
    {
        public class WeatherClient
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
    
            private readonly HttpClient client;
    
            public WeatherClient(HttpClient client)
            {
                this.client = client;
            }
    
            public async Task<WeatherForecast[]> GetWeatherAsync()
            {
                var responseMessage = await this.client.GetAsync("/weatherforecast");
                var stream = await responseMessage.Content.ReadAsStreamAsync();
                return await JsonSerializer.DeserializeAsync<WeatherForecast[]>(stream, options);
            }
        }
    }
   ```

4. Now register this client in `Startup.cs` class in `ConfigureServices` of the `frontend` project:

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

5. Add a `Forecasts` property to the `Index` page model under `Pages\Index.cshtml.cs` in the `frontend` project.

    ```C#
    public WeatherForecast[] Forecasts { get; set; }
    ```

   Change the `OnGet` method to take the `WeatherClient` to call the `backend` service and store the result in the `Forecasts` property:

   ```C#
   public async Task OnGet([FromServices]WeatherClient client)
   {
        Forecasts = await client.GetWeatherAsync();
   }
   ```

6. Change the `Index.cshtml` razor view to render the `Forecasts` property in the razor page:

   ```cshtml
   @page
   @model IndexModel
   @{
        ViewData["Title"] = "Home page";
    }

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

7. Run the project and the `frontend` service should be able to successfully call the `backend` service!

    When you visit the `frontend` service you should see a table of weather data. This data was produced randomly in the `backend` service. The fact that you're seeing it in a web UI in the `frontend` means that the services are able to communicate.

## Next Steps

Now that you are able to run a multi-project application with [`tye run`](commandline/tye-run.md), move on to the [Frontend Backend Deploy Sample](frontend_backed_deploy.md) to learn how to deploy this application to Kubernetes.
