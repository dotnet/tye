// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.Server;
using RabbitMQ.Client;

namespace Backend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization();

            services.AddCodeFirstGrpc();

            services.AddSingleton(sp =>
            {
                AmqpTcpEndpoint endpoint;
                var connectionString = Configuration["connectionstrings:rabbit"];
                if (connectionString == null)
                {
                    var host = Configuration["service:rabbit:host"];
                    var port = int.Parse(Configuration["service:rabbit:port"]);
                    endpoint = new AmqpTcpEndpoint(host, port);
                }
                else
                {
                    endpoint = new AmqpTcpEndpoint(new Uri(connectionString));
                }

                var factory = new ConnectionFactory()
                {
                    Endpoint = endpoint,
                };
                var connection = factory.CreateConnection();
                var channel = connection.CreateModel();

                channel.QueueDeclare(queue: "orders",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
                return channel;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<OrdersService>();
            });
        }
    }
}
