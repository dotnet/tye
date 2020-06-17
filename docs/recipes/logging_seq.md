# Logging with Seq

Seq is a popular log-aggregation system that gives you a powerful log search and dashboard engine with views across all of your services.

Tye can push logs to Seq easily without the need for any SDKs or code changes in your services.

## Getting started: running locally with Seq

> :bulb: If you want an existing sample to run, the [sample here](https://github.com/dotnet/tye/tree/master/samples/frontend-backend) will do. This recipe will show examples of UI and data based on that sample. You own application will work fine, but the data and examples will look different.

The first step is to add the `seq` extension to your `tye.yaml`. Add the `extensions` node and its children from the example below.

```yaml
name: frontend-backend

extensions:
- name: seq
  logPath: ./.logs

services:
- name: backend
  project: backend/backend.csproj
- name: frontend
  project: frontend/frontend.csproj
```

The `logPath` property here configures the path where Seq will store its data.

Now launch the application with Tye:

```sh
tye run
```

If you navigate to the Tye dashboard you should see an extra service (`seq`) in the list of services.


<img width="1103" alt="image" src="https://user-images.githubusercontent.com/1769935/83251452-f26ffa00-a1ec-11ea-9642-29e4ec579178.png">

Visit the first URI (`http://localhost:5341`) in your browser to access the Seq dashboard.

Now you're ready to view the data! After it loads, it should look like the screenshot below:

<img width="1515" alt="image" src="https://user-images.githubusercontent.com/1769935/83251005-4cbc8b00-a1ec-11ea-9c76-b7e6db2ef73b.png">

Now you can see the logs from your application with each field broken out into structured format. If you take advantage of [structured logging](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1#log-message-template) then you'll see your own data included in structured form here alongside framework logs.

It should like the screenshot below:

<img width="1101" alt="image" src="https://user-images.githubusercontent.com/1769935/83252026-e46ea900-a1ed-11ea-9b96-38695c42dab4.png">
