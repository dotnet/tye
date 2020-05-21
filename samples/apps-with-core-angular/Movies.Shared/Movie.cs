using System;
using System.Collections.Generic;
using System.Text;

namespace Movies.Shared
{
   public class Movie
    {
        public int MovieId { get; set; }
        public string MovieName { get; set; }
        public string DirectorName { get; set; }
        public int ReleaseYear { get; set; }
    }
}
