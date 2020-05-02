# Distributed Tracing with Zipkin

> :warning: This recipe refers to features that are only available in our CI builds at the moment. These features will be part of the 0.2 release on nuget.org "soon".

Distributed tracing is a key diagnostics tool in your microservices toolbelt. Distributed traces show you at a glance what operations took place across your entire application to complete some task.

Zipkin is a popular open-source distributed trace storage and query system. It can show you:

- Which services were involved with an end-to-end operation?
- What are the trace IDs to reference logs for an operation?
- What were the timings of work done in each service for an operation?

## Getting Started: Enabling WC3 tracing

The first step is to enable the W3C trace format in your .NET applications. **This is mandatory, you won't get traces without doing this!**

> :bulb: If you want an existing sample to run, the [sample here](https://github.com/dotnet/tye/tree/master/samples/frontend-backend) will do. This sample code already initializes the trace format.

You need to place the following statement somewhere early in your program:

```C#
Activity.DefaultIdFormat = ActivityIdFormat.W3C;
```

Here's what it would look like for a typical ASP.NET Core application in `Program.cs` (recommended).

```C#
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Frontend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
```

## Enabling Zipkin in tye.yaml

The next step is to add the `zipkin` extension to your `tye.yaml`. Add the `extensions` node and its children from the example below.

```yaml
name: frontend-backend

extensions:
- name: zipkin

services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
```

## Running with zipkin

That's all the required configuration. Next, launch the application with `tye run`.

The dashboard should show that you've got a `zipkin` service running.

<img width="1514" alt="image" src="https://user-images.githubusercontent.com/1430011/80853097-d162bc00-8be2-11ea-884b-a04b52103931.png">

Visit the `frontend` service (or send some traffic to your services if you are using your own code). This will populate some data in the zipkin instance.

Then visit the zipkin dashboard by clicking the link. Tye will use a fixed address of `http://localhost:9411` which is typically used by zipkin.

You won't see anything at first... because you need to do a search.

<img width="1356" alt="image" src="https://user-images.githubusercontent.com/1430011/80853176-5bab2000-8be3-11ea-92c6-e8c187c57a38.png">

Click the magnifying glass icon (on the right) to do a search.

<img width="1359" alt="image" src="https://user-images.githubusercontent.com/1430011/80853255-218e4e00-8be4-11ea-848c-4d55febb096f.png">

Clicking on one of these traces can show you a breakdown of how time was spent, what URIs were accessed, status codes, etc.

<img width="1203" alt="image" src="https://user-images.githubusercontent.com/1430011/80853303-98c3e200-8be4-11ea-8d33-23f49200bbb4.png">

