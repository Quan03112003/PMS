import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}
  get<T = any>(path: string): Observable<T> { return this.http.get<T>(environment.apiUrl + path); }
  getText(path: string): Observable<string> { return this.http.get(environment.apiUrl + path, { responseType: 'text' }); }
  post<T = any>(path: string, body: unknown): Observable<T> { return this.http.post<T>(environment.apiUrl + path, body); }
  postForm<T = any>(path: string, body: FormData): Observable<T> { return this.http.post<T>(environment.apiUrl + path, body); }
  delete<T = any>(path: string): Observable<T> { return this.http.delete<T>(environment.apiUrl + path); }
  postFile<T = any>(path: string, file: File): Observable<T> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<T>(environment.apiUrl + path, body);
  }
  patch<T = any>(path: string, body: unknown): Observable<T> { return this.http.patch<T>(environment.apiUrl + path, body); }
  put<T = any>(path: string, body: unknown): Observable<T> { return this.http.put<T>(environment.apiUrl + path, body); }
}
