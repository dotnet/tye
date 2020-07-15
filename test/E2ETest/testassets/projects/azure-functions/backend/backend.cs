using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace backend
{
    public static class backend
    {
        [FunctionName("backend")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var backendInfo = new BackendInfo()
            {
                IP = req.HttpContext.Connection.LocalIpAddress.ToString(),
                Hostname = System.Net.Dns.GetHostName(),
            };
            
            return new OkObjectResult(backendInfo);
        }

        class BackendInfo
        {
            public string IP { get; set; } = default!;

            public string Hostname { get; set; } = default!;
        }
    }
}
