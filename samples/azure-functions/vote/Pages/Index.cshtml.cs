using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues; // Namespace for Queue storage types
using Azure.Storage.Queues.Models; // Namespace for PeekedMessage
using System.Text;

namespace Vote.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        public IConfiguration Configuration { get; set; }

        [BindProperty()]
        public string Vote {get;set;}

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            Configuration = configuration;
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

                _logger.LogInformation($"pushing {data}");

                var plainTextBytes = Encoding.UTF8.GetBytes(data);

                QueueClient queueClient = new QueueClient("UseDevelopmentStorage=true", "test-queue");
                queueClient.CreateIfNotExists();
                await queueClient.SendMessageAsync(Convert.ToBase64String(plainTextBytes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting vote.");
            }
        }
    }
}
