import { Injectable, inject, signal, computed } from '@angular/core';
import { Subscription, interval } from 'rxjs';

import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class SessionTimeoutService {
  private readonly authService = inject(AuthService);

  private readonly currentTime = signal(Date.now());

  private timerSubscription: Subscription | null = null;

  readonly remainingSeconds = computed(() => {
    const perfil = this.authService.currentProfile();

    if (!perfil) {
      return null;
    }

    const expiracionInactividad = this.parseUtc(
      perfil.sesion.fechaExpiracionInactividad
    );

    const expiracionSesion = this.parseUtc(
      perfil.sesion.fechaExpiracion
    );

    const expiracion = Math.min(
      expiracionInactividad,
      expiracionSesion
    );

    if (!Number.isFinite(expiracion)) {
      return null;
    }

    return Math.max(
      0,
      Math.ceil((expiracion - this.currentTime()) / 1000)
    );
  });

  readonly showWarning = computed(() => {
    const perfil = this.authService.currentProfile();
    const segundos = this.remainingSeconds();

    return perfil !== null &&
      segundos !== null &&
      segundos > 0 &&
      segundos <= perfil.sesion.advertenciaInactividadSegundos;
  });

  readonly isExpired = computed(() =>
    this.remainingSeconds() === 0
  );

  start(): void {
    if (this.timerSubscription) {
      return;
    }

    this.currentTime.set(Date.now());

    this.timerSubscription = interval(1000).subscribe(() => {
      this.currentTime.set(Date.now());
    });
  }

  stop(): void {
    this.timerSubscription?.unsubscribe();
    this.timerSubscription = null;
  }

  private parseUtc(value: string): number {
    const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value);

    return Date.parse(
      hasTimezone ? value : `${value}Z`
    );
  }
}