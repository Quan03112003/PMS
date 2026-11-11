import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => next(req).pipe(
  catchError((error: HttpErrorResponse) => {
    if (error.status === 401 && req.url.startsWith('/api')) localStorage.removeItem('pms_token');
    return throwError(() => error);
  })
);
