import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
  provideRouter
} from '@angular/router';
import {
  of,
  throwError,
  isObservable,
  firstValueFrom
} from 'rxjs';
import { vi } from 'vitest';

import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('AuthGuard', () => {
  let router: Router;

  const perfilSimulado = {
    id: 1,
    nombre: 'Administrador',
    apellido: 'Sistema',
    roles: [],
    permisos: []
  };

  const isAuthenticated = signal(false);

  const currentProfile = signal<
    typeof perfilSimulado | null
  >(null);

  const refreshMock = vi.fn();
  const getProfileMock = vi.fn();

  beforeEach(async () => {
    isAuthenticated.set(false);
    currentProfile.set(null);

    refreshMock.mockReset();
    getProfileMock.mockReset();

    getProfileMock.mockImplementation(() => {
      currentProfile.set(perfilSimulado);

      return of(perfilSimulado);
    });

    await TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated,
            currentProfile,
            refresh: refreshMock,
            getProfile: getProfileMock
          }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
  });

  async function ejecutarGuard() {
    const resultado = TestBed.runInInjectionContext(() =>
      authGuard(
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot
      )
    );

    return isObservable(resultado)
      ? firstValueFrom(resultado)
      : resultado;
  }

  it('should allow access when authenticated with profile', async () => {
    isAuthenticated.set(true);
    currentProfile.set(perfilSimulado);

    const resultado = await ejecutarGuard();

    expect(resultado).toBe(true);
    expect(refreshMock).not.toHaveBeenCalled();
    expect(getProfileMock).not.toHaveBeenCalled();
  });

  it('should load the profile when authenticated', async () => {
    isAuthenticated.set(true);

    const resultado = await ejecutarGuard();

    expect(resultado).toBe(true);
    expect(refreshMock).not.toHaveBeenCalled();
    expect(getProfileMock).toHaveBeenCalledTimes(1);
    expect(currentProfile()).toEqual(perfilSimulado);
  });

  it('should restore the session and load the profile', async () => {
    refreshMock.mockImplementation(() => {
      isAuthenticated.set(true);

      return of({
        accessToken: 'token-renovado'
      });
    });

    const resultado = await ejecutarGuard();

    expect(resultado).toBe(true);
    expect(refreshMock).toHaveBeenCalledTimes(1);
    expect(getProfileMock).toHaveBeenCalledTimes(1);
    expect(isAuthenticated()).toBe(true);
    expect(currentProfile()).toEqual(perfilSimulado);
  });

  it('should redirect to login when refresh fails', async () => {
    refreshMock.mockReturnValue(
      throwError(() => ({
        status: 401
      }))
    );

    const resultado = await ejecutarGuard();

    expect(resultado).toEqual(
      router.createUrlTree(['/login'])
    );

    expect(refreshMock).toHaveBeenCalledTimes(1);
    expect(getProfileMock).not.toHaveBeenCalled();
    expect(isAuthenticated()).toBe(false);
  });

  it('should redirect when refresh does not restore the session', async () => {
    refreshMock.mockReturnValue(of({}));

    const resultado = await ejecutarGuard();

    expect(resultado).toEqual(
      router.createUrlTree(['/login'])
    );

    expect(isAuthenticated()).toBe(false);
    expect(getProfileMock).not.toHaveBeenCalled();
  });

  it('should redirect when loading the profile fails', async () => {
    isAuthenticated.set(true);

    getProfileMock.mockReturnValue(
      throwError(() => ({
        status: 500
      }))
    );

    const resultado = await ejecutarGuard();

    expect(resultado).toEqual(
      router.createUrlTree(['/login'])
    );

    expect(getProfileMock).toHaveBeenCalledTimes(1);
    expect(currentProfile()).toBeNull();
  });

  it('should redirect when the profile is not stored', async () => {
    isAuthenticated.set(true);

    getProfileMock.mockReturnValue(of(perfilSimulado));

    const resultado = await ejecutarGuard();

    expect(resultado).toEqual(
      router.createUrlTree(['/login'])
    );

    expect(currentProfile()).toBeNull();
  });
});