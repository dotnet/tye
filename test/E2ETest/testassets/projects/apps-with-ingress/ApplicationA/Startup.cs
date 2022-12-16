using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApplicationA
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWebSockets();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello from Application A " + Environment.GetEnvironmentVariable("APP_INSTANCE") ?? Environment.GetEnvironmentVariable("HOSTNAME"));
                    await context.Response.WriteAsync(context.Request.Path);
                });

                endpoints.MapGet("/C/test", async context =>
                {
                    await context.Response.WriteAsync("Hit path /C/test");
                });

                // This method returns the body content and query string back to the caller, to test that the ingress passes those properly
                endpoints.MapPost("/data", async context =>
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var content = await reader.ReadToEndAsync();
                    var query = context.Request.QueryString.Value;

                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        content,
                        query
                    }));
                });

                endpoints.MapGet("/ws", async context =>
                {
                    if (!context.WebSockets.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                    }
                    else
                    {
                        using var ws = await context.WebSockets.AcceptWebSocketAsync();
                        await Echo(ws);
                    }
                });

                async Task Echo(WebSocket webSocket)
                {
                    var buffer = new byte[1024 * 4];
                    var result = await webSocket.ReceiveAsync(buffer.AsMemory(), default);
                    while (result.MessageType != WebSocketMessageType.Close)
                    {
                        await webSocket.SendAsync(buffer.AsMemory(..result.Count), result.MessageType, result.EndOfMessage, default);
                        result = await webSocket.ReceiveAsync(buffer.AsMemory(), default);
                    }
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default);
                }
            });
        }
    }
}
