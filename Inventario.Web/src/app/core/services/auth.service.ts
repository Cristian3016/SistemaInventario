import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  Observable,
  EMPTY,
  tap,
  map,
  finalize,
  shareReplay,
  defer,
  catchError,
  throwError,
  ignoreElements,
  endWith,
  switchMap
} from 'rxjs';

import { environment } from '../../../environments/environment';
import { LoginDto, LoginRespuestaDto } from '../../models/auth.model';
import { UsuarioAutenticadoDto } from '../../models/usuario-autenticado.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/auth`;

  private readonly session = signal<LoginRespuestaDto | null>(null);
  private readonly profile = signal<UsuarioAutenticadoDto | null>(null);

  private refreshInProgress: Observable<LoginRespuestaDto> | null = null;
  private logoutInProgress: Observable<void> | null = null;

  readonly currentUser = this.session.asReadonly();

  readonly currentProfile = computed(() => {
    const perfil = this.profile();
    const sesion = this.session();

    return perfil && sesion && perfil.sesion.id === sesion.sesionId
      ? perfil
      : null;
  });

  readonly accessToken = computed(() =>
    this.session()?.accessToken ?? null
  );

  readonly isAuthenticated = computed(() =>
    this.accessToken() !== null
  );

  readonly roles = computed(() =>
    this.currentProfile()?.roles ?? []
  );

  readonly permissions = computed(() =>
    this.currentProfile()?.permisos ?? []
  );

  login(credenciales: LoginDto): Observable<LoginRespuestaDto> {
    return this.http.post<LoginRespuestaDto>(
      `${this.apiUrl}/login`,
      credenciales,
      {
        withCredentials: true
      }
    ).pipe(
      tap(respuesta => {
        this.profile.set(null);
        this.session.set(respuesta);
      })
    );
  }

  getProfile(): Observable<UsuarioAutenticadoDto> {
    return defer(() => {
      const sesionId = this.session()?.sesionId;
      return this.http.get<UsuarioAutenticadoDto>(
        `${this.apiUrl}/me`
      ).pipe(
        tap(respuesta => {
          if (
            sesionId &&
            this.session()?.sesionId === sesionId &&
            respuesta.sesion.id === sesionId
          ) {
            this.profile.set(respuesta);
          }
        })
      );
    });
  }
  registerActivity(): Observable<void> {
  return this.http.post<void>(
    `${this.apiUrl}/actividad`,
    null
  );
}
  refresh(): Observable<LoginRespuestaDto> {
    return defer(() => {
      if (this.logoutInProgress) {
        return throwError(() =>
          new Error('El cierre de sesión está en curso.')
        );
      }

      if (!this.refreshInProgress) {
        const sesionAnterior = this.session();

        this.refreshInProgress = this.http.post<LoginRespuestaDto>(
          `${this.apiUrl}/refresh`,
          null,
          {
            headers: {
              'X-CSRF-Protection': '1'
            },
            withCredentials: true
          }
        ).pipe(
          tap(respuesta => this.session.set(respuesta)),
          catchError(error => {
            if (
              error.status === 401 &&
              this.session() === sesionAnterior
            ) {
              this.clearSession();
            }

            return throwError(() => error);
          }),
          finalize(() => {
            this.refreshInProgress = null;
          }),
          shareReplay({
            bufferSize: 1,
            refCount: false
          })
        );
      }

      return this.refreshInProgress;
    });
  }

  logout(): Observable<void> {
    return defer(() => {
      if (!this.logoutInProgress) {
        const renovacionPendiente =
          this.refreshInProgress ?? EMPTY;

        this.logoutInProgress = renovacionPendiente.pipe(
          ignoreElements(),
          catchError(() => EMPTY),
          endWith(undefined),
          switchMap(() =>
            this.http.post(
              `${this.apiUrl}/logout`,
              null,
              {
                headers: {
                  'X-CSRF-Protection': '1'
                },
                withCredentials: true,
                responseType: 'text'
              }
            )
          ),
          tap(() => this.clearSession()),
          map(() => undefined),
          finalize(() => {
            this.logoutInProgress = null;
          }),
          shareReplay({
            bufferSize: 1,
            refCount: false
          })
        );
      }

      return this.logoutInProgress;
    });
  }

  hasPermission(codigo: string): boolean {
    return this.permissions().some(
      permiso => permiso.codigo === codigo
    );
  }

  hasRole(nombre: string): boolean {
    return this.roles().some(
      rol => rol.nombre === nombre
    );
  }

  clearSession(): void {
    this.profile.set(null);
    this.session.set(null);
  }
} 