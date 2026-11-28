import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';

import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const sessionReady$ = authService.isAuthenticated()
    ? of(true)
    : authService.refresh().pipe(
        map(() => authService.isAuthenticated())
      );

  return sessionReady$.pipe(
    switchMap(isAuthenticated => {
      if (!isAuthenticated) {
        return of(router.createUrlTree(['/login']));
      }

      if (authService.currentProfile()) {
        return of(true);
      }

      return authService.getProfile().pipe(
        map(() =>
          authService.currentProfile()
            ? true
            : router.createUrlTree(['/login'])
        )
      );
    }),
    catchError(() =>
      of(router.createUrlTree(['/login']))
    )
  );
};