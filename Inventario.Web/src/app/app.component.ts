import {
  Component,
  inject,
  effect,
  untracked,
  DestroyRef,
  signal,
  computed
} from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, finalize } from 'rxjs';

import { AuthService } from './core/services/auth.service';
import { SessionActivityService } from './core/services/session-activity.service';
import { SessionTimeoutService } from './core/services/session-timeout.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class App {
  private readonly authService = inject(AuthService);
  private readonly sessionActivityService = inject(SessionActivityService);
  private readonly sessionTimeoutService = inject(SessionTimeoutService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly remainingSeconds = this.sessionTimeoutService.remainingSeconds;

  readonly showWarning = computed(() =>
    this.authService.isAuthenticated() &&
    (
      this.sessionTimeoutService.showWarning() ||
      this.sessionTimeoutService.isExpired()
    )
  );

  readonly sessionAction = signal<'continue' | 'logout' | null>(null);
  readonly sessionWarningError = signal('');

  private wasAuthenticated = false;

  constructor() {
    effect(() => {
      const isAuthenticated = this.authService.isAuthenticated();
      const warningVisible = this.showWarning();
      const isExpired = this.sessionTimeoutService.isExpired();
      const currentAction = this.sessionAction();

      const sessionEnded =
        this.wasAuthenticated && !isAuthenticated;

      this.wasAuthenticated = isAuthenticated;

      untracked(() => {
        if (isAuthenticated) {
          this.sessionTimeoutService.start();

          if (warningVisible || isExpired) {
            this.sessionActivityService.stop();
          } else {
            this.sessionActivityService.start();
            this.sessionWarningError.set('');
          }

          if (isExpired && !currentAction) {
            this.expireSession();
          }
        } else {
          this.sessionActivityService.stop();
          this.sessionTimeoutService.stop();
          this.sessionWarningError.set('');
        }

        if (sessionEnded) {
          const currentUrl = this.router.url;

          const destinationUrl = this.router
            .currentNavigation()
            ?.extractedUrl.toString();

          if (
            currentUrl !== '/login' &&
            destinationUrl !== '/login'
          ) {
            void this.router.navigate(['/login'], {
              replaceUrl: true
            });
          }
        }
      });
    });

    this.destroyRef.onDestroy(() => {
      this.sessionActivityService.stop();
      this.sessionTimeoutService.stop();
    });
  }

  continueSession(): void {
    if (
      this.sessionAction() ||
      !this.authService.isAuthenticated() ||
      !this.showWarning() ||
      this.sessionTimeoutService.isExpired()
    ) {
      return;
    }

    this.sessionWarningError.set('');
    this.sessionAction.set('continue');

    this.authService.registerActivity().pipe(
      switchMap(() => this.authService.getProfile()),
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.sessionAction.set(null))
    ).subscribe({
      error: () => {
        this.sessionWarningError.set(
          'No se pudo extender la sesión. Intentá nuevamente.'
        );
      }
    });
  }

  logoutSession(): void {
    if (this.sessionAction()) {
      return;
    }

    this.sessionWarningError.set('');
    this.sessionAction.set('logout');

    this.authService.logout().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.sessionAction.set(null))
    ).subscribe({
      error: () => {
        this.sessionWarningError.set(
          'No se pudo cerrar la sesión. Intentá nuevamente.'
        );
      }
    });
  }

  private expireSession(): void {
    if (
      this.sessionAction() ||
      !this.authService.isAuthenticated()
    ) {
      return;
    }

    this.sessionWarningError.set('');
    this.sessionAction.set('logout');

    this.authService.logout().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.sessionAction.set(null))
    ).subscribe({
      error: () => {
        this.authService.clearSession();
      }
    });
  }
}