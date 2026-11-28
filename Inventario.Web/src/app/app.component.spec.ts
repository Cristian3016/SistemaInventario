import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { App } from './app.component';
import { AuthService } from './core/services/auth.service';
import { SessionActivityService } from './core/services/session-activity.service';
import { SessionTimeoutService } from './core/services/session-timeout.service';

describe('App', () => {
  let router: Router;

  const isAuthenticated = signal(false);
  const remainingSeconds = signal<number | null>(null);
  const showWarning = signal(false);
  const isExpired = signal(false);

  const logoutMock = vi.fn();
  const clearSessionMock = vi.fn();
  const activityStartMock = vi.fn();
  const activityStopMock = vi.fn();
  const timeoutStartMock = vi.fn();
  const timeoutStopMock = vi.fn();

  beforeEach(async () => {
    vi.resetAllMocks();

    isAuthenticated.set(false);
    remainingSeconds.set(null);
    showWarning.set(false);
    isExpired.set(false);

    logoutMock.mockImplementation(() => {
      isAuthenticated.set(false);
      return of(undefined);
    });

    clearSessionMock.mockImplementation(() => {
      isAuthenticated.set(false);
    });

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated,
            logout: logoutMock,
            clearSession: clearSessionMock
          }
        },
        {
          provide: SessionActivityService,
          useValue: {
            start: activityStartMock,
            stop: activityStopMock
          }
        },
        {
          provide: SessionTimeoutService,
          useValue: {
            remainingSeconds,
            showWarning,
            isExpired,
            start: timeoutStartMock,
            stop: timeoutStopMock
          }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the router outlet', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('router-outlet')).not.toBeNull();
  });

  it('should not redirect when initially unauthenticated', () => {
    const navigateMock = vi
      .spyOn(router, 'navigate')
      .mockResolvedValue(true);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('should redirect to login when the session ends', () => {
    isAuthenticated.set(true);

    vi.spyOn(router, 'url', 'get')
      .mockReturnValue('/dashboard');

    const navigateMock = vi
      .spyOn(router, 'navigate')
      .mockResolvedValue(true);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    isAuthenticated.set(false);
    fixture.detectChanges();

    expect(navigateMock).toHaveBeenCalledTimes(1);

    expect(navigateMock).toHaveBeenCalledWith(
      ['/login'],
      { replaceUrl: true }
    );
  });

  it('should display the session expiration warning', () => {
    isAuthenticated.set(true);
    remainingSeconds.set(45);
    showWarning.set(true);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(
      compiled.querySelector('.session-warning-dialog')
    ).not.toBeNull();

    expect(
      compiled.querySelector('.session-warning-countdown')
        ?.textContent?.trim()
    ).toBe('45');

    expect(activityStopMock).toHaveBeenCalled();
  });

  it('should automatically logout when the session expires', () => {
    isAuthenticated.set(true);

    vi.spyOn(router, 'url', 'get')
      .mockReturnValue('/dashboard');

    const navigateMock = vi
      .spyOn(router, 'navigate')
      .mockResolvedValue(true);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    remainingSeconds.set(0);
    isExpired.set(true);

    fixture.detectChanges();
    fixture.detectChanges();

    expect(logoutMock).toHaveBeenCalledTimes(1);
    expect(isAuthenticated()).toBe(false);

    expect(navigateMock).toHaveBeenCalledWith(
      ['/login'],
      { replaceUrl: true }
    );
  });

  it('should clear the local session when automatic logout fails', () => {
    isAuthenticated.set(true);

    logoutMock.mockReturnValue(
      throwError(() => ({ status: 500 }))
    );

    vi.spyOn(router, 'navigate')
      .mockResolvedValue(true);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    remainingSeconds.set(0);
    isExpired.set(true);

    fixture.detectChanges();
    fixture.detectChanges();

    expect(logoutMock).toHaveBeenCalledTimes(1);
    expect(clearSessionMock).toHaveBeenCalledTimes(1);
    expect(isAuthenticated()).toBe(false);
  });
});