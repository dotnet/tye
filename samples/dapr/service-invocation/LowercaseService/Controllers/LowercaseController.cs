using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared;

namespace LowercaseService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LowercaseController : ControllerBase
    {
        private readonly ILogger<LowercaseController> _logger;

        public LowercaseController(ILogger<LowercaseController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult<ConvertedResult> Get(string sentence)
        {
            _logger.LogInformation($"{GetType().Name} triggered with sentence={sentence}");
            return new ConvertedResult()
            {
                Original = sentence,
                Sentence = sentence.ToLower()
            };
        }
    }
}
