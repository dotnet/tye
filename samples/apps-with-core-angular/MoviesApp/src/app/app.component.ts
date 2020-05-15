import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { IMovie } from './models/movie';
import { MoviesService } from './movies.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {

  title = 'Movies Microservice Demo using Tye Project';
  movies: IMovie[];


  constructor(private moviesService: MoviesService) { }

  ngOnInit() {
    this.getMovies();
  }

  getMovies() {
    this.moviesService.getMovies().subscribe((movies: IMovie[])  => {
      this.movies = movies;
      console.log(this.movies);
    }, error => console.log(error));
  }
}
