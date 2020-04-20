// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Worker
{
    public class QueueWorker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<QueueWorker> _logger;

        public QueueWorker(ILogger<QueueWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                var queue = await ConnectAsync(cancellationToken);
                queue.QueueDeclare(
                    queue: "orders",
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new EventingBasicConsumer(queue);
                consumer.Received += (model, ea) =>
                {
                    // Use the raw log API to avoid formatting the JSON string (since it has {})
                    var text = Encoding.UTF8.GetString(ea.Body);
                    _logger.Log(LogLevel.Information, 0, "Dequeued " + text, exception: null, formatter: (m, e) => m);
                };

                queue.BasicConsume(
                    queue: "orders",
                    autoAck: true,
                    consumer: consumer);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Failed to start listening to rabbit mq");
                throw;
            }
        }

        private async Task<IModel> ConnectAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    var uri = _configuration.GetServiceUri("rabbit");
                    var endpoint = new AmqpTcpEndpoint(uri);

                    var factory = new ConnectionFactory()
                    {
                        Endpoint = endpoint,
                    };

                    var connection = factory.CreateConnection();
                    return connection.CreateModel();
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "Failed to start listening to rabbit mq");
                }

                // Rely on the Task.Delay to throw and exit the loop if we're still waiting for connection
                // when shutdown happens.
                await Task.Delay(5000, cancellationToken);
            }
        }
    }
}
