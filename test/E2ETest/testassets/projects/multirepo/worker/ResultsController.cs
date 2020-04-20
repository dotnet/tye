using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Worker
{
    [ApiController]
    public class ResultsController : ControllerBase
    {
        private IConfiguration _configuration;

        public ResultsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("/results")]
        public async Task<IEnumerable<VoteCount>> Get()
        {
            using (var connection = new NpgsqlConnection(_configuration.GetConnectionString("postgres")))
            {
                connection.Open();

                return await connection.QueryAsync<VoteCount>("SELECT Vote, COUNT(Id) AS Count FROM votes GROUP BY Vote ORDER BY Vote");
            }
        }
    }
}
