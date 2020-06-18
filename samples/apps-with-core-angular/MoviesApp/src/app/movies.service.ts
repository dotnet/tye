import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class MoviesService {
  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getMovies() {
    return this.http.get(this.baseUrl + 'movies');
  }
}
