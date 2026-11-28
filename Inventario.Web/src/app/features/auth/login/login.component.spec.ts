import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';

import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let httpTesting: HttpTestingController;

  const navigateMock = vi.fn().mockResolvedValue(true);
  const loginUrl = 'https://localhost:7076/api/auth/login';

  beforeEach(async () => {
    navigateMock.mockClear();

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: Router,
          useValue: {
            navigate: navigateMock
          }
        }
      ]
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should create the login component', () => {
    expect(component).toBeTruthy();
  });

  it('should reject an empty form', () => {
    expect(component.loginForm.invalid).toBe(true);
  });

  it('should validate completed credentials', () => {
    component.loginForm.setValue({
      username: 'usuarioPrueba',
      password: 'Prueba1234'
    });

    expect(component.loginForm.valid).toBe(true);
  });

  it('should mark empty fields as touched', () => {
    component.onSubmit();

    expect(component.loginForm.controls.username.touched).toBe(true);
    expect(component.loginForm.controls.password.touched).toBe(true);

    httpTesting.expectNone(loginUrl);

    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('should hide the password by default', () => {
    expect(component.showPassword()).toBe(false);
  });

  it('should toggle password visibility', () => {
    component.togglePassword();

    expect(component.showPassword()).toBe(true);

    component.togglePassword();

    expect(component.showPassword()).toBe(false);
  });

  it('should authenticate successfully and navigate to dashboard', () => {
    component.loginForm.setValue({
      username: 'usuarioPrueba',
      password: 'Prueba1234'
    });

    component.showPassword.set(true);
    component.onSubmit();

    expect(component.isLoading()).toBe(true);

    const request = httpTesting.expectOne(loginUrl);

    expect(request.request.method).toBe('POST');

    expect(request.request.body).toEqual({
      nombreUsuario: 'usuarioPrueba',
      password: 'Prueba1234'
    });

    request.flush({
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
    });

    expect(component.loginSuccess()).toBe(true);
    expect(component.isLoading()).toBe(false);
    expect(component.errorMessage()).toBe('');
    expect(component.showPassword()).toBe(false);

    expect(component.loginForm.getRawValue()).toEqual({
      username: '',
      password: ''
    });

    expect(navigateMock).toHaveBeenCalledWith(['/dashboard']);
  });

  it('should handle invalid credentials without navigating', () => {
    component.loginForm.setValue({
      username: 'usuarioPrueba',
      password: 'incorrecta'
    });

    component.onSubmit();

    const request = httpTesting.expectOne(loginUrl);

    request.flush(null, {
      status: 401,
      statusText: 'Unauthorized'
    });

    expect(component.loginSuccess()).toBe(false);
    expect(component.isLoading()).toBe(false);

    expect(component.errorMessage())
      .toBe('Usuario o contraseña incorrectos.');

    expect(navigateMock).not.toHaveBeenCalled();
  });
});