import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}
  private headers(): HttpHeaders {
    const token = localStorage.getItem('pms_token');
    return new HttpHeaders(token ? { Authorization: `Bearer ${token}` } : {});
  }
  get<T = any>(path: string): Observable<T> { return this.http.get<T>('/api' + path, { headers: this.headers() }); }
  getText(path: string): Observable<string> { return this.http.get('/api' + path, { headers: this.headers(), responseType: 'text' }); }
  post<T = any>(path: string, body: unknown): Observable<T> { return this.http.post<T>('/api' + path, body, { headers: this.headers() }); }
  postFile<T = any>(path: string, file: File): Observable<T> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<T>('/api' + path, body, { headers: this.headers() });
  }
  patch<T = any>(path: string, body: unknown): Observable<T> { return this.http.patch<T>('/api' + path, body, { headers: this.headers() }); }
}
