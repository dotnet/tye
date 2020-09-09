using System;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;

namespace worker_function
{
    public class QueueTrigger
    {
        private readonly IConfiguration _configuration;

        public QueueTrigger(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("QueueTrigger")]
        public async Task Run([QueueTrigger("test-queue", Connection = "AzureWebJobsStorage")]string data, ILogger log)
        {
            log.LogInformation(_configuration.GetConnectionString("postgres"));
            using (var connection = new NpgsqlConnection(_configuration.GetConnectionString("postgres")))
            {
                var vote = JsonSerializer.Deserialize<Vote>(data);
                // TODO
                connection.Open();
                await connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS votes (
                                                Id VARCHAR(255) NOT NULL UNIQUE,
                                                Vote VARCHAR(255) NOT NULL);");
                                                
                var command = @"INSERT INTO votes (Id, Vote) VALUES (@voterId, @vote)
                                ON CONFLICT (Id)
                                DO UPDATE SET Vote = @vote";

                await connection.ExecuteAsync(command, vote);
            }

            log.LogInformation($"C# Queue trigger function processed: {data}");
        }

        public class Vote
        {
            public Guid voterId { get; set; }
            public string vote { get; set; }
        }

        public class VoteCount
        {
            public string Vote { get; set; }
            public int Count { get; set; }
        }
    }
}
