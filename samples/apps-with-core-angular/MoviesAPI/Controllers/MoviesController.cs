using Microsoft.AspNetCore.Mvc;

namespace MoviesAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesController : ControllerBase
    {
        private readonly MoviesService _moviesService;
        public MoviesController(MoviesService moviesService)
            => _moviesService = moviesService;

        [HttpGet]
        public IActionResult GetBooks()
            => Ok(_moviesService.GetMovies());
    }
}
