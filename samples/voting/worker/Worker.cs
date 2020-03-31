using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Dapper;
using System.Text.Json;
using Npgsql;
using Microsoft.AspNetCore.SignalR;

namespace Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ResultsHub> _hubContext;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, IHubContext<ResultsHub> hubContext)
        {
            _logger = logger;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started running at: {time}", DateTimeOffset.Now);

            try
            {
                using (var connection = new NpgsqlConnection(_configuration.GetSqlConnectionString()))
                using (var redisConnection = ConnectionMultiplexer.Connect(GetRedisConnectionString(_configuration)))
                {
                    await Task.Delay(1000);

                    connection.Open();
                    var redis = redisConnection.GetDatabase();

                    await CreateTable(connection);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var data = await redis.ListRightPopAsync("votes");

                            if (data.HasValue)
                            {
                                var vote = JsonSerializer.Deserialize<Vote>(data);

                                _logger.LogInformation($"Got new vote from redis {vote.vote} {vote.voterId}");

                                var command = @"INSERT INTO votes (Id, Vote) VALUES (@voterId, @vote)
                                                ON CONFLICT (Id)
                                                DO UPDATE SET Vote = @vote";

                                await connection.ExecuteAsync(command, vote);

                                var newResults = await connection.QueryAsync<VoteCount>("SELECT Vote, COUNT(Id) AS Count FROM votes GROUP BY Vote ORDER BY Vote");

                                _logger.LogInformation("Wrote results to postgres, calling other clients with updated results.");
                                await _hubContext.Clients.All.SendAsync("votesRecieved", newResults);
                            }
                            else
                            {
                                var newResults = await connection.QueryAsync<VoteCount>("SELECT Vote, COUNT(Id) AS Count FROM votes GROUP BY Vote ORDER BY Vote");
                                await _hubContext.Clients.All.SendAsync("votesRecieved", newResults);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "error processing vote.");
                        }

                        await Task.Delay(100, stoppingToken);
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Exception starting worker");
            }
        }

        private class Vote
        {
            public Guid voterId { get; set; }
            public string vote { get; set; }
        }

        private async Task CreateTable(IDbConnection connection)
        {
            await connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS votes (
                                                Id VARCHAR(255) NOT NULL UNIQUE,
                                                Vote VARCHAR(255) NOT NULL);");
        }

        private string GetRedisConnectionString(IConfiguration configuration)
        {
            var connectionString = configuration["connectionstring:redis"];
            if (connectionString != null)
            {
                return connectionString;
            }

            return $"{configuration["service:redis:host"]}:{configuration["service:redis:port"]}";
        }
    }
}
