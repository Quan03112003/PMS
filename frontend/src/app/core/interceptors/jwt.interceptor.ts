import { HttpInterceptorFn } from '@angular/common/http';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api')) return next(req);
  const token = localStorage.getItem('pms_token');
  const headers: Record<string, string> = { 'Accept-Language': localStorage.getItem('pms_lang') || 'vi' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return next(req.clone({ setHeaders: headers }));
};
