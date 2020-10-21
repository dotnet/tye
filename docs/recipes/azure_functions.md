# Using Tye and Azure Functions

[Azure Functions](https://azure.microsoft.com/en-us/services/functions/) is a popular serverless compute platform from Azure. Tye supports running Azure functions locally.

## Getting Started: Create an Azure Function

Starting from the [sample here](https://github.com/dotnet/tye/tree/master/samples/frontend-backend), we are going to transform the backend from a web application to an azure function app.

To start, create an Azure Function project in a folder called `backend-function`. You can do this via:
- [Visual Studio Code](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-first-function-vs-code?pivots=programming-language-csharp)
- [Visual Studio](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-your-first-function-visual-studio)
- [Commandline](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-first-azure-function-azure-cli?tabs=bash%2Cbrowser&pivots=programming-language-csharp)

Next, you must have the [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash). You can install the core tools by installing the [standalone installer](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash#v2) or through [npm](https://www.npmjs.com/package/azure-functions-core-tools).

You can also specify a path to func by specifying `pathToFunc` for the azure function service.

Next, create an HttpTrigger called `MyHttpTrigger` in your functions project. Change the contents of MyHttpTrigger to the following:

```c#
        [FunctionName("MyHttpTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var backendInfo = new BackendInfo()
            {
                IP = req.HttpContext.Connection.LocalIpAddress.ToString(),
                Hostname = System.Net.Dns.GetHostName(),
            };

            return new OkObjectResult(backendInfo);
        }

        class BackendInfo
        {
            public string IP { get; set; } = default!;

            public string Hostname { get; set; } = default!;
        }
```

Finally, change line in the frontend's `Startup.cs` to call the right endpoint (line 63), changing "/" to "api/MyHttpTrigger".

```c#
endpoints.MapGet("/", async context =>
{
    var bytes = await httpClient.GetByteArrayAsync("/api/MyHttpTrigger");
    var backendInfo = JsonSerializer.Deserialize<BackendInfo>(bytes, options);
    ...
}
```

## Adding your Azure Function in tye.yaml

Now that we have a backend function added, you can simply modify your tye.yaml to point to the azure function instead:

```yaml
# tye application configuration file
# read all about it at https://github.com/dotnet/tye
name: frontend-backend
services:
- name: backend
  azureFunction: backend-function/ # folder path to the azure function.
- name: frontend
  project: frontend/frontend.csproj
```

## Running locally

You can now run the application locally by doing `tye run`.

On first run of an app that requires functions, tye will install any tools necessary to run functions apps in the future. This may take a while on first run, but will be saved afterwards.

Navigate to the tye dashboard to see both the frontend and backend running. Navigate to the frontend to see that the app still has the same behavior as before.

## Deployment

Deployment of azure functions is currently not supported.
