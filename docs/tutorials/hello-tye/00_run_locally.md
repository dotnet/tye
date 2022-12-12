# Frontend Backend sample with tye run

This tutorial will demonstrate how to use [`tye run`](/docs/reference/commandline/tye-run.md) to run a multi-project application. If you haven't done so already, follow the [Getting Started Instructions](/docs/getting_started.md) to install tye.

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
    - The `Bindings` column has links to the listening URLs of the service.
    
    Navigate to the `frontend` service using one of the urls on the dashboard in the *Bindings* column. It should be in the form of <http://localhost:[port]> or <https://localhost:[port]>.

    The dashboard will use port 8000 if possible. Services written using ASP.NET Core will have their listening ports assigned randomly if not explicitly configured.

## Running multiple applications with tye run

1. If you haven't already, stop the existing `tye run` command using `Ctrl + C`. Create a backend API that the frontend will call inside of the `microservices/` folder.

    ```text
    dotnet new webapi -n backend
    ```

1. Create a solution file and add both projects

    ```text
    dotnet new sln
    dotnet sln add frontend backend
    ```

    You should have a solution called `microservice.sln` that references the `frontend` and `backend` projects.

2. Run the `tye` command line in the folder with the solution.

    ```text
    tye run
    ```

    The dashboard should show both the `frontend` and `backend` services. You can navigate to both of them through either the dashboard of the url outputted by `tye run`.

    > :warning: The `backend` service in this example was created using the `webapi` project template and will return an HTTP 404 for its root URL.

## Getting the frontend to communicate with the backend

Now that we have two applications running, let's make them communicate. By default, `tye` enables service discovery by injecting environment variables with a specific naming convention. For more information on, see [service discovery](/docs/reference/service_discovery.md).

1. If you haven't already, stop the existing `tye run` command using `Ctrl + C`. Open the solution in your editor of choice.

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
   using System.Text.Json;
   using System.Net.Http.Json;

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
               return await this.client.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");
           }
       }
   }
   ```

4. Add a reference to the `Microsoft.Tye.Extensions.Configuration` package to the frontend project

    ```txt
    dotnet add frontend/frontend.csproj package Microsoft.Tye.Extensions.Configuration  --version "0.4.0-*"
    ```

5. Now register this client in `frontend` by adding the following to the existing code in the `Program.cs` file:

   ```C#
   ...
   
   services.AddRazorPages();
   /** Add the following to wire the client to the backend **/
   services.AddHttpClient<WeatherClient>(client =>
   {
            client.BaseAddress = builder.Configuration.GetServiceUri("backend");
   });
   /** End added code **/
   ...
   ```

   This will wire up the `WeatherClient` to use the correct URL for the `backend` service.

6. Add a `Forecasts` property to the `Index` page model under `Pages\Index.cshtml.cs` in the `frontend` project.

    ```C#
    ...
    public WeatherForecast[] Forecasts { get; set; }
    ...
    ```

   Change the `OnGet` method to take the `WeatherClient` to call the `backend` service and store the result in the `Forecasts` property:

   ```C#
   ...
   public async Task OnGet([FromServices]WeatherClient client)
   {
        Forecasts = await client.GetWeatherAsync();
   }
   ...
   ```

7. Change the `Index.cshtml` razor view to render the `Forecasts` property in the razor page:

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

8.  Run the project with [`tye run`](/docs/reference/commandline/tye-run.md) and the `frontend` service should be able to successfully call the `backend` service!

    When you visit the `frontend` service you should see a table of weather data. This data was produced randomly in the `backend` service. The fact that you're seeing it in a web UI in the `frontend` means that the services are able to communicate. Unfortunately, this doesn't work out of the box on Linux
    right now due to how self-signed certificates are handled, please see the workaround [below](#troubleshooting)

## Next Steps

Now that you are able to run a multi-project application with [`tye run`](/docs/reference/commandline/tye-run.md), move on to [the next step (deploy)](01_deploy.md) to learn how to deploy this application to Kubernetes.


## Troubleshooting

### Certificate is invalid exception on Linux
`dotnet dev-certs ...` doesn't fully work on Linux so you need to generate and trust your own certificate.

#### Generate the certificate
```sh
# See https://stackoverflow.com/questions/55485511/how-to-run-dotnet-dev-certs-https-trust
# for more details

cat << EOF > localhost.conf
[req]
default_bits       = 2048
default_keyfile    = localhost.key
distinguished_name = req_distinguished_name
req_extensions     = req_ext
x509_extensions    = v3_ca

[req_distinguished_name]
commonName                  = Common Name (e.g. server FQDN or YOUR name)
commonName_default          = localhost
commonName_max              = 64

[req_ext]
subjectAltName = @alt_names

[v3_ca]
subjectAltName = @alt_names
basicConstraints = critical, CA:false
keyUsage = keyCertSign, cRLSign, digitalSignature,keyEncipherment

[alt_names]
DNS.1   = localhost
DNS.2   = 127.0.0.1

EOF

# Generate certificate from config
openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout localhost.key -out localhost.crt \
    -config localhost.conf

# Export pfx
openssl pkcs12 -export -out localhost.pfx -inkey localhost.key -in localhost.crt

# Import CA as trusted
sudo cp localhost.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates 

# Validate the certificate
openssl verify localhost.crt
```

Once you have this working, copy `localhost.pfx` into the `backend` directory, then add the following
to `appsettings.json`

```json
{
  ...
  "Kestrel": {
    "Certificates": {
      "Default": {
        "Path": "localhost.pfx",
        "Password": ""
      }
    }
  }
}
```

You may still get an untrusted warning with your browser but it will work with dotnet.
