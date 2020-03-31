using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Vote.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private IConnectionMultiplexer _redisConnection;
        public IConfiguration Configuration { get; set; }

        [BindProperty()]
        public string Vote {get;set;}

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration, IConnectionMultiplexer redisConnection)
        {
            _logger = logger;
            Configuration = configuration;
            _redisConnection = redisConnection;
        }

        public async Task OnPost()
        {
            try
            {
                var voterId = TempData.Peek("VoterId");
                if (voterId == null)
                {
                    voterId = Guid.NewGuid();
                    TempData["VoterId"] = voterId;
                }

                var data = JsonSerializer.Serialize(new { voterId = voterId, vote = Vote });

                var database = _redisConnection.GetDatabase();
                _logger.LogInformation($"pushing {data}");
                await database.ListRightPushAsync("votes", data);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error submitting vote.");
            }
        }
    }
}
