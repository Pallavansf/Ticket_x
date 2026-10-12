import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isApiRequest = request.url.startsWith(environment.apiBaseUrl) || request.url.startsWith('/api/');
  const session = auth.isSessionValid() ? auth.session() : null;
  let outgoing = request;
  if (isApiRequest && session?.accessToken) {
    outgoing = outgoing.clone({ setHeaders: { Authorization: `Bearer ${session.accessToken}` } });
  }
  if (isApiRequest && !outgoing.headers.has('X-Correlation-ID')) {
    outgoing = outgoing.clone({ setHeaders: { 'X-Correlation-ID': crypto.randomUUID() } });
  }
  return next(outgoing).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      auth.logout();
      if (router.url !== '/login') void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
    }
    return throwError(() => error);
  }));
};

