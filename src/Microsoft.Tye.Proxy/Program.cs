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

                        logger.LogInformation("Received connection information {Host}:{Port}", containerHost, Environment.GetEnvironmentVariable("PROXY_PORT"));

                        static (int Port, int ExternalPort) ResolvePort(string portValue)
                        {
                            var pair = portValue.Split(':');
                            return (int.Parse(pair[0]), int.Parse(pair[1]));
                        }

                        var ports = Environment.GetEnvironmentVariable("PROXY_PORT")?.Split(';').Select(ResolvePort) ?? Enumerable.Empty<(int, int)>();

                        server.UseSockets(sockets =>
                        {
                            foreach (var mapping in ports)
                            {
                                sockets.Listen(IPAddress.Any, mapping.Port, o =>
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

                                            logger.LogDebug("Attempting to connect to {ServiceName} listening on {Port}:{ExternalPort}", serviceName, mapping.Port, mapping.ExternalPort);

                                            await target.ConnectAsync(containerHost, mapping.ExternalPort);

                                            logger.LogDebug("Successfully connected to {ServiceName} listening on {Port}:{ExternalPort}", serviceName, mapping.Port, mapping.ExternalPort);

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
                                            logger.LogDebug("Proxying traffic to {ServiceName} {Port}:{ExternalPort}", serviceName, mapping.Port, mapping.ExternalPort);

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
