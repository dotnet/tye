using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Dapper;
using System.Text.Json;

namespace worker_function
{
    public class GetResults
    {
        private readonly IConfiguration _configuration;

        public GetResults(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("GetResults")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation(_configuration.GetConnectionString("postgres"));
            using (var connection = new NpgsqlConnection(_configuration.GetConnectionString("postgres")))
            {
                var newResults = await connection.QueryAsync<QueueTrigger.VoteCount>("SELECT Vote, COUNT(Id) AS Count FROM votes GROUP BY Vote ORDER BY Vote");
                return new OkObjectResult(JsonSerializer.Serialize(newResults));
            }
        }
    }
}
