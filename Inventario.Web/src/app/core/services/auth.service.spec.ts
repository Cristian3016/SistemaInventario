import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { AuthService } from './auth.service';
import { LoginRespuestaDto } from '../../models/auth.model';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;

  const apiUrl = `${environment.apiUrl}/auth`;

  const respuesta: LoginRespuestaDto = {
    usuarioId: 1,
    nombre: 'Usuario',
    apellido: 'Prueba',
    nombreUsuario: 'usuarioPrueba',
    sesionId: '00000000-0000-4000-8000-000000000001',
    accessToken: 'token-simulado',
    tokenType: 'Bearer',
    fechaExpiracionAccessToken: '2026-12-01T12:00:00Z',
    fechaExpiracionSesion: '2026-12-01T18:00:00Z',
    fechaExpiracionInactividad: '2026-12-01T11:30:00Z',
    advertenciaInactividadSegundos: 60
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  function iniciarSesion(): void {
    service.login({
      nombreUsuario: 'usuarioPrueba',
      password: 'Prueba1234'
    }).subscribe();

    const request = httpTesting.expectOne(`${apiUrl}/login`);

    expect(request.request.method).toBe('POST');

    request.flush(respuesta);
  }

  it('should store the authenticated session', () => {
    iniciarSesion();

    expect(service.currentUser()).toEqual(respuesta);
    expect(service.accessToken()).toBe('token-simulado');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should clear the local session', () => {
    iniciarSesion();

    service.clearSession();

    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should logout and clear the session', () => {
    iniciarSesion();

    service.logout().subscribe();

    const request = httpTesting.expectOne(`${apiUrl}/logout`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();

    request.flush('');

    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should preserve the session when logout fails', () => {
    iniciarSesion();

    let errorStatus: number | undefined;

    service.logout().subscribe({
      error: error => {
        errorStatus = error.status;
      }
    });

    const request = httpTesting.expectOne(`${apiUrl}/logout`);

    request.flush('Error del servidor', {
      status: 500,
      statusText: 'Internal Server Error'
    });

    expect(errorStatus).toBe(500);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.accessToken()).toBe('token-simulado');
  });
  it('should refresh and update the session', () => {
  iniciarSesion();

  const sesionActual = service.currentUser()!;

  const respuestaRenovada = {
    ...sesionActual,
    accessToken: 'nuevo-access-token'
  };

  service.refresh().subscribe();

  const request = httpTesting.expectOne(`${apiUrl}/refresh`);

  expect(request.request.method).toBe('POST');
  expect(request.request.body).toBeNull();
  expect(request.request.withCredentials).toBe(true);

  expect(request.request.headers.get('X-CSRF-Protection'))
    .toBe('1');

  request.flush(respuestaRenovada);

  expect(service.currentUser()).toEqual(respuestaRenovada);
  expect(service.accessToken()).toBe('nuevo-access-token');
  expect(service.isAuthenticated()).toBe(true);
});
it('should share concurrent refresh requests', () => {
  iniciarSesion();

  const sesionActual = service.currentUser()!;

  const respuestaRenovada = {
    ...sesionActual,
    accessToken: 'nuevo-access-token'
  };

  const respuestas: string[] = [];

  service.refresh().subscribe(respuesta => {
    respuestas.push(respuesta.accessToken);
  });

  service.refresh().subscribe(respuesta => {
    respuestas.push(respuesta.accessToken);
  });

  const request = httpTesting.expectOne(`${apiUrl}/refresh`);

  expect(request.request.method).toBe('POST');

  request.flush(respuestaRenovada);

  expect(respuestas).toEqual([
    'nuevo-access-token',
    'nuevo-access-token'
  ]);

  expect(service.accessToken()).toBe('nuevo-access-token');

  httpTesting.expectNone(`${apiUrl}/refresh`);
});
it('should allow a new refresh after completion', () => {
  iniciarSesion();

  const sesionActual = service.currentUser()!;

  service.refresh().subscribe();

  const primeraSolicitud = httpTesting.expectOne(
    `${apiUrl}/refresh`
  );

  primeraSolicitud.flush({
    ...sesionActual,
    accessToken: 'access-token-renovado-1'
  });

  expect(service.accessToken()).toBe(
    'access-token-renovado-1'
  );

  service.refresh().subscribe();

  const segundaSolicitud = httpTesting.expectOne(
    `${apiUrl}/refresh`
  );

  segundaSolicitud.flush({
    ...sesionActual,
    accessToken: 'access-token-renovado-2'
  });

  expect(service.accessToken()).toBe(
    'access-token-renovado-2'
  );

  httpTesting.expectNone(`${apiUrl}/refresh`);
});
it('should release refresh after an error', () => {
  iniciarSesion();

  const sesionActual = service.currentUser()!;
  let estadoError = 0;

  service.refresh().subscribe({
    error: error => {
      estadoError = error.status;
    }
  });

  const primeraSolicitud = httpTesting.expectOne(
    `${apiUrl}/refresh`
  );

  primeraSolicitud.flush(
    { mensaje: 'Solicitud no permitida.' },
    {
      status: 403,
      statusText: 'Forbidden'
    }
  );

  expect(estadoError).toBe(403);
  expect(service.currentUser()).toEqual(sesionActual);

  service.refresh().subscribe();

  const segundaSolicitud = httpTesting.expectOne(
    `${apiUrl}/refresh`
  );

  segundaSolicitud.flush({
    ...sesionActual,
    accessToken: 'access-token-recuperado'
  });

  expect(service.accessToken()).toBe(
    'access-token-recuperado'
  );

  httpTesting.expectNone(`${apiUrl}/refresh`);
});
it('should clear the session when refresh returns 401', () => {
  iniciarSesion();

  let estadoError = 0;

  expect(service.isAuthenticated()).toBe(true);

  service.refresh().subscribe({
    error: error => {
      estadoError = error.status;
    }
  });

  const request = httpTesting.expectOne(
    `${apiUrl}/refresh`
  );

  request.flush(
    { mensaje: 'La sesión no es válida o ha expirado.' },
    {
      status: 401,
      statusText: 'Unauthorized'
    }
  );

  expect(estadoError).toBe(401);
  expect(service.currentUser()).toBeNull();
  expect(service.accessToken()).toBeNull();
  expect(service.isAuthenticated()).toBe(false);
});
it('should wait for refresh before logging out', () => {
  iniciarSesion();

  const sesionActual = service.currentUser()!;
  let logoutCompletado = false;

  service.refresh().subscribe();

  const refreshRequest = httpTesting.expectOne(
    `${apiUrl}/refresh`
  );

  service.logout().subscribe(() => {
    logoutCompletado = true;
  });

  httpTesting.expectNone(`${apiUrl}/logout`);

  expect(logoutCompletado).toBe(false);

  refreshRequest.flush({
    ...sesionActual,
    accessToken: 'access-token-renovado'
  });

  const logoutRequest = httpTesting.expectOne(
    `${apiUrl}/logout`
  );

  expect(logoutRequest.request.method).toBe('POST');
  expect(service.accessToken()).toBe('access-token-renovado');

  logoutRequest.flush(null, {
    status: 204,
    statusText: 'No Content'
  });

  expect(logoutCompletado).toBe(true);
  expect(service.currentUser()).toBeNull();
  expect(service.accessToken()).toBeNull();
  expect(service.isAuthenticated()).toBe(false);
});
it('should block refresh while logout is in progress', () => {
  iniciarSesion();

  let mensajeError = '';

  service.logout().subscribe();

  const logoutRequest = httpTesting.expectOne(
    `${apiUrl}/logout`
  );

  service.refresh().subscribe({
    error: error => {
      mensajeError = error.message;
    }
  });

  expect(mensajeError).toBe(
    'El cierre de sesión está en curso.'
  );

  httpTesting.expectNone(`${apiUrl}/refresh`);

  logoutRequest.flush(null, {
    status: 204,
    statusText: 'No Content'
  });

  expect(service.currentUser()).toBeNull();
  expect(service.accessToken()).toBeNull();
  expect(service.isAuthenticated()).toBe(false);
});
it('should share concurrent logout requests', () => {
  iniciarSesion();

  let cierresCompletados = 0;

  service.logout().subscribe(() => {
    cierresCompletados++;
  });

  service.logout().subscribe(() => {
    cierresCompletados++;
  });

  const request = httpTesting.expectOne(
    `${apiUrl}/logout`
  );

  expect(request.request.method).toBe('POST');
  expect(cierresCompletados).toBe(0);

  request.flush(null, {
    status: 204,
    statusText: 'No Content'
  });

  expect(cierresCompletados).toBe(2);
  expect(service.currentUser()).toBeNull();
  expect(service.accessToken()).toBeNull();
  expect(service.isAuthenticated()).toBe(false);

  httpTesting.expectNone(`${apiUrl}/logout`);
});
});