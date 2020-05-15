using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Movies.Shared;

namespace MoviesAPI
{
    public class MoviesService
    {
        private readonly ILogger _logger;

        public MoviesService(ILogger<MoviesService> logger)
            => _logger = logger;

        private List<Movie> _movies = new List<Movie>()
        {
            new Movie { MovieId = 1, MovieName = "Die Another Day", DirectorName = "Lee Tamahori", ReleaseYear = 2002},
            new Movie { MovieId = 2, MovieName = "Top Gun", DirectorName = "Tony Scott", ReleaseYear = 1986},
            new Movie { MovieId = 3, MovieName = "Jurassic Park", DirectorName = "Steven Spielberg", ReleaseYear = 1993},
            new Movie { MovieId = 4, MovieName = "Independence Day", DirectorName = "Roland Emmerich", ReleaseYear = 1996},
            new Movie { MovieId = 5, MovieName = "Tomorrow Never Dies", DirectorName = "Roger Spottiswoode", ReleaseYear = 1997},
        };

        public IEnumerable<Movie> GetMovies()
        {
            _logger.LogInformation("GetBooks invoked");
            return _movies;
        }
    }
}
