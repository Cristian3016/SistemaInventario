import { signal, computed } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

describe('AuthInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;

  const token = signal<string | null>(null);
  const apiUrl = environment.apiUrl;

  const currentUser = computed(() =>
    token() ? { sesionId: 'sesion-prueba' } : null
  );

  const refreshMock = vi.fn();

  beforeEach(() => {
    token.set(null);
    refreshMock.mockReset();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([authInterceptor])
        ),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            accessToken: token,
            currentUser,
            refresh: refreshMock
          }
        }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should add the JWT to API requests', () => {
    token.set('token-simulado');

    http.get(`${apiUrl}/productos`).subscribe();

    const request = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(request.request.headers.get('Authorization'))
      .toBe('Bearer token-simulado');

    request.flush([]);
  });

  it('should not add a token when unauthenticated', () => {
    http.get(`${apiUrl}/productos`).subscribe();

    const request = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(request.request.headers.has('Authorization'))
      .toBe(false);

    request.flush([]);
  });

  it('should not add the JWT to login requests', () => {
    token.set('token-simulado');

    http.post(`${apiUrl}/auth/login`, {
      nombreUsuario: 'usuarioPrueba',
      password: 'Prueba1234'
    }).subscribe();

    const request = httpTesting.expectOne(
      `${apiUrl}/auth/login`
    );

    expect(request.request.headers.has('Authorization'))
      .toBe(false);

    request.flush({});
  });

  it('should not add the JWT to refresh requests', () => {
    token.set('token-simulado');

    http.post(`${apiUrl}/auth/refresh`, null, {
      headers: {
        'X-CSRF-Protection': '1'
      },
      withCredentials: true
    }).subscribe();

    const request = httpTesting.expectOne(
      `${apiUrl}/auth/refresh`
    );

    expect(request.request.method).toBe('POST');

    expect(request.request.headers.has('Authorization'))
      .toBe(false);

    expect(request.request.headers.get('X-CSRF-Protection'))
      .toBe('1');

    expect(request.request.withCredentials).toBe(true);

    request.flush({});
  });

  it('should not add the JWT to external requests', () => {
    token.set('token-simulado');

    const externalUrl = 'https://example.com/api/productos';

    http.get(externalUrl).subscribe();

    const request = httpTesting.expectOne(externalUrl);

    expect(request.request.headers.has('Authorization'))
      .toBe(false);

    request.flush([]);
  });

  it('should not add the JWT to unrelated API paths', () => {
    token.set('token-simulado');

    const unrelatedUrl = `${apiUrl}-externa/productos`;

    http.get(unrelatedUrl).subscribe();

    const request = httpTesting.expectOne(unrelatedUrl);

    expect(request.request.headers.has('Authorization'))
      .toBe(false);

    request.flush([]);
  });

  it('should refresh the token and retry after a 401', () => {
    token.set('token-expirado');

    refreshMock.mockImplementation(() => {
      token.set('token-renovado');

      return of({
        accessToken: 'token-renovado'
      });
    });

    let resultado: unknown;

    http.get(`${apiUrl}/productos`).subscribe(response => {
      resultado = response;
    });

    const primeraSolicitud = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(
      primeraSolicitud.request.headers.get('Authorization')
    ).toBe('Bearer token-expirado');

    primeraSolicitud.flush(
      { mensaje: 'Token expirado.' },
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    expect(refreshMock).toHaveBeenCalledTimes(1);

    const segundaSolicitud = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(
      segundaSolicitud.request.headers.get('Authorization')
    ).toBe('Bearer token-renovado');

    segundaSolicitud.flush([
      { id: 1, nombre: 'Martillo' }
    ]);

    expect(resultado).toEqual([
      { id: 1, nombre: 'Martillo' }
    ]);

    httpTesting.expectNone(`${apiUrl}/productos`);
  });

  it('should not retry when refresh fails', () => {
    token.set('token-expirado');

    refreshMock.mockImplementation(() => {
      token.set(null);

      return throwError(() => ({
        status: 401,
        message: 'La sesión ha expirado.'
      }));
    });

    let estadoError = 0;

    http.get(`${apiUrl}/productos`).subscribe({
      error: error => {
        estadoError = error.status;
      }
    });

    const request = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    request.flush(
      { mensaje: 'Token expirado.' },
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    expect(refreshMock).toHaveBeenCalledTimes(1);
    expect(estadoError).toBe(401);
    expect(token()).toBeNull();

    httpTesting.expectNone(`${apiUrl}/productos`);
  });
    it('should not refresh again when the retried request returns 401', () => {
    token.set('token-expirado');

    refreshMock.mockImplementation(() => {
      token.set('token-renovado');

      return of({
        accessToken: 'token-renovado'
      });
    });

    let estadoError = 0;

    http.get(`${apiUrl}/productos`).subscribe({
      error: error => {
        estadoError = error.status;
      }
    });

    const primeraSolicitud = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    primeraSolicitud.flush(
      { mensaje: 'Token expirado.' },
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    const segundaSolicitud = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(
      segundaSolicitud.request.headers.get('Authorization')
    ).toBe('Bearer token-renovado');

    segundaSolicitud.flush(
      { mensaje: 'Solicitud no autorizada.' },
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    expect(refreshMock).toHaveBeenCalledTimes(1);
    expect(estadoError).toBe(401);

    httpTesting.expectNone(`${apiUrl}/productos`);
  });
    it('should reuse an already refreshed token', () => {
    token.set('token-expirado');

    let resultado: unknown;

    http.get(`${apiUrl}/productos`).subscribe(response => {
      resultado = response;
    });

    const primeraSolicitud = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(
      primeraSolicitud.request.headers.get('Authorization')
    ).toBe('Bearer token-expirado');

    token.set('token-renovado');

    primeraSolicitud.flush(
      { mensaje: 'Token expirado.' },
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    expect(refreshMock).not.toHaveBeenCalled();

    const segundaSolicitud = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    expect(
      segundaSolicitud.request.headers.get('Authorization')
    ).toBe('Bearer token-renovado');

    segundaSolicitud.flush([
      { id: 1, nombre: 'Martillo' }
    ]);

    expect(resultado).toEqual([
      { id: 1, nombre: 'Martillo' }
    ]);

    httpTesting.expectNone(`${apiUrl}/productos`);
  });
    it('should not refresh the token when logout returns 401', () => {
    token.set('token-expirado');

    let estadoError = 0;

    http.post(`${apiUrl}/auth/logout`, null, {
      headers: {
        'X-CSRF-Protection': '1'
      },
      withCredentials: true
    }).subscribe({
      error: error => {
        estadoError = error.status;
      }
    });

    const request = httpTesting.expectOne(
      `${apiUrl}/auth/logout`
    );

    expect(request.request.headers.get('Authorization'))
      .toBe('Bearer token-expirado');

    request.flush(
      { mensaje: 'La sesión no es válida.' },
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    expect(refreshMock).not.toHaveBeenCalled();
    expect(estadoError).toBe(401);

    httpTesting.expectNone(`${apiUrl}/auth/refresh`);
    httpTesting.expectNone(`${apiUrl}/auth/logout`);
  });
    it('should not refresh the token when a request returns 403', () => {
    token.set('token-valido');

    let estadoError = 0;

    http.get(`${apiUrl}/productos`).subscribe({
      error: error => {
        estadoError = error.status;
      }
    });

    const request = httpTesting.expectOne(
      `${apiUrl}/productos`
    );

    request.flush(
      { mensaje: 'No tiene permisos para realizar esta operación.' },
      {
        status: 403,
        statusText: 'Forbidden'
      }
    );

    expect(estadoError).toBe(403);
    expect(refreshMock).not.toHaveBeenCalled();
    expect(token()).toBe('token-valido');

    httpTesting.expectNone(`${apiUrl}/auth/refresh`);
    httpTesting.expectNone(`${apiUrl}/productos`);
  });
});