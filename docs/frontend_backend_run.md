# Frontend Backend sample with tye run

This tutorial will demonstrate how to use `tye run` to run a multi-project application. If you haven't so already, follow the [Getting Started Instructions](getting_started.md) to install tye.

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

    The dashboard should show the `frontend` application running. You should be able to view the application logs and you should be able to hit navigate to the application in your browser.

## Running multiple applications with tye run

1. Create a backend API that the frontend will call inside of the `microservices/` folder.

    ```text
    dotnet new webapi -n backend
    ```

1. Create a solution file and add both projects

    ```text
    dotnet new sln
    dotnet sln add frontend
    dotnet sln add backend
    ```

    You should have a solution called `microservice.sln` that references the `frontend` and `backend` projects.

1. If you haven't already, stop the existing `tye run` command using `Ctrl + C`. Run the `tye` command line in the folder with the solution.

    ```text
    tye run
    ```

    The dashboard should show both the `frontend` and `backend` services. You can navigate to both of them through either the dashboard of the url outputted by `tye run`.

## Getting the frontend to communicate with the backend

Now that we have two applications running, let's make them communicate. By default, `tye` enables service discovery by injecting environment variables with a specific naming convention.

1. Add a `GetUri()` method to the frontend project at the bottom of the Startup.cs class:

    ```C#
    private Uri GetUri(IConfiguration configuration, string name)
    {
        return new Uri($"http://{configuration[$"service:{name}:host"]}:{configuration[$"service:{name}:port"]}");
    }
    ```

    This method resolved the URL using the `tye` naming convention for services. For more information on this, see the [Service Definition](service_discovery.md).

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

    This will match the backend `WeatherForecast.cs`.

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

   Change the `OnGet` method to take the `WeatherClient` to call the `backend` service and store the result in the `Forecasts` property:

   ```C#
   public async Task OnGet([FromServices]WeatherClient client)
   {
        Forecasts = await client.GetWeatherAsync();
   }
   ```

1. Change the `Index.cshtml` razor view to render the `Forecasts` property in the razor page:

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

## Next Steps

Now that you are able to run a multi-project application with `tye run`, see the [Frontend Backend Deploy Sample](frontend_backed_deploy.md) to learn how to deploy this application to Kubernetes.
