import { inject } from '@angular/core';
import {
  HttpErrorResponse,
  HttpInterceptorFn
} from '@angular/common/http';
import {
  catchError,
  switchMap,
  throwError
} from 'rxjs';

import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const token = authService.accessToken();

  const isApiRequest = request.url.startsWith(
    `${environment.apiUrl}/`
  );

  const isLoginRequest =
    request.url === `${environment.apiUrl}/auth/login`;

  const isRefreshRequest =
    request.url === `${environment.apiUrl}/auth/refresh`;

  const isLogoutRequest =
    request.url === `${environment.apiUrl}/auth/logout`;

  if (!token || !isApiRequest || isLoginRequest || isRefreshRequest) {
    return next(request);
  }

  const sessionId = authService.currentUser()?.sesionId;

  const authenticatedRequest = request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (
        error.status !== 401 ||
        isLogoutRequest ||
        !sessionId ||
        authService.currentUser()?.sesionId !== sessionId
      ) {
        return throwError(() => error);
      }

      const currentToken = authService.accessToken();

      if (currentToken && currentToken !== token) {
        return next(
          request.clone({
            setHeaders: {
              Authorization: `Bearer ${currentToken}`
            }
          })
        );
      }

      return authService.refresh().pipe(
        switchMap(() => {
          const newToken = authService.accessToken();

          if (
            !newToken ||
            authService.currentUser()?.sesionId !== sessionId
          ) {
            return throwError(() => error);
          }

          return next(
            request.clone({
              setHeaders: {
                Authorization: `Bearer ${newToken}`
              }
            })
          );
        })
      );
    })
  );
};