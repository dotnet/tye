using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.Proxy
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var serviceName = Environment.GetEnvironmentVariable("APP_INSTANCE");
            var containerHost = Environment.GetEnvironmentVariable("CONTAINER_HOST");

            using var host = new HostBuilder()
                    .ConfigureLogging(logging =>
                    {
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Debug);
                    })
                    .ConfigureServer(server =>
                    {
                        var logger = server.ApplicationServices.GetRequiredService<ILogger<Program>>();

                        logger.LogInformation("Received connection information {Host}:{Port}", containerHost, Environment.GetEnvironmentVariable("PORT"));

                        var ports = Environment.GetEnvironmentVariable("PORT")?.Split(';').Select(int.Parse) ?? Enumerable.Empty<int>();

                        server.UseSockets(sockets =>
                        {
                            foreach (var port in ports)
                            {
                                sockets.Listen(IPAddress.Any, port, o =>
                                {
                                    // o.UseConnectionLogging("Microsoft.Tye.Proxy");

                                    o.Run(async connection =>
                                    {
                                        var notificationFeature = connection.Features.Get<IConnectionLifetimeNotificationFeature>();

                                        NetworkStream? targetStream = null;

                                        try
                                        {
                                            var target = new Socket(SocketType.Stream, ProtocolType.Tcp)
                                            {
                                                NoDelay = true
                                            };

                                            logger.LogDebug("Attempting to connect to {ServiceName} listening on {ExternalPort}:{Port}", serviceName, port, port);

                                            await target.ConnectAsync(containerHost, port);

                                            logger.LogDebug("Successfully connected to {ServiceName} listening on {ExternalPort}:{Port}", serviceName, serviceName, port);

                                            targetStream = new NetworkStream(target, ownsSocket: true);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogDebug(ex, "Proxy error for service {ServiceName}", serviceName);

                                            if (targetStream is object)
                                            {
                                                await targetStream.DisposeAsync();
                                            }

                                            connection.Abort();
                                            return;
                                        }

                                        try
                                        {
                                            logger.LogDebug("Proxying traffic to {ServiceName} {ExternalPort}:{InternalPort}", serviceName, port, port);

                                            // external -> internal
                                            var reading = Task.Run(() => connection.Transport.Input.CopyToAsync(targetStream, notificationFeature.ConnectionClosedRequested));

                                            // internal -> external
                                            var writing = Task.Run(() => targetStream.CopyToAsync(connection.Transport.Output, notificationFeature.ConnectionClosedRequested));

                                            await Task.WhenAll(reading, writing);
                                        }
                                        catch (ConnectionResetException)
                                        {
                                            // Connection was reset
                                        }
                                        catch (IOException)
                                        {
                                            // Reset can also appear as an IOException with an inner SocketException
                                        }
                                        catch (OperationCanceledException ex)
                                        {
                                            if (!notificationFeature.ConnectionClosedRequested.IsCancellationRequested)
                                            {
                                                logger.LogDebug(0, ex, "Proxy error for service {ServiceName}", serviceName);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogDebug(0, ex, "Proxy error for service {ServiceName}", serviceName);
                                        }
                                        finally
                                        {
                                            await targetStream.DisposeAsync();
                                        }

                                        // This needs to reconnect to the target port(s) until its bound
                                        // it has to stop if the service is no longer running
                                    });

                                });
                            }
                        });
                    })
                    .Build();

            await host.RunAsync();
        }
    }
}
