import { Injectable, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import {
  EMPTY,
  Subscription,
  fromEvent,
  merge,
  filter,
  throttleTime,
  exhaustMap,
  switchMap,
  catchError
} from 'rxjs';

import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class SessionActivityService {
  private readonly authService = inject(AuthService);
  private readonly document = inject(DOCUMENT);

  private readonly activityInterval = 60000;

  private activitySubscription: Subscription | null = null;

  start(): void {
    if (this.activitySubscription) {
      return;
    }

    this.activitySubscription = merge(
      fromEvent(this.document, 'pointerdown'),
      fromEvent(this.document, 'keydown'),
      fromEvent(this.document, 'wheel')
    ).pipe(
      filter(() => this.authService.isAuthenticated()),
      throttleTime(this.activityInterval),
      exhaustMap(() => {
        const sesionId =
          this.authService.currentUser()?.sesionId;

        return this.authService.registerActivity().pipe(
          switchMap(() =>
            sesionId &&
            this.authService.currentUser()?.sesionId === sesionId
              ? this.authService.getProfile()
              : EMPTY
          ),
          catchError(() => EMPTY)
        );
      })
    ).subscribe();
  }

  stop(): void {
    this.activitySubscription?.unsubscribe();
    this.activitySubscription = null;
  }
}