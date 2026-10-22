import { TestBed } from '@angular/core/testing';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { authGuard, guestGuard, permissionGuard } from './auth.guards';
import { AuthService } from '../auth/auth.service';

describe('auth guards', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));
  it('redirects anonymous users and allows signed-in users', () => {
    const auth = TestBed.inject(AuthService); const router = TestBed.inject(Router);
    const state = { url: '/app' } as never;
    expect(runInInjectionContext(TestBed.inject(EnvironmentInjector), () => authGuard({} as never, state))).toEqual(router.createUrlTree(['/login'], { queryParams: { returnUrl: '/app' } }));
    auth.setBearerToken(`${btoa('{}')}.${btoa(JSON.stringify({ sub: '1' }))}.x`);
    expect(runInInjectionContext(TestBed.inject(EnvironmentInjector), () => authGuard({} as never, state))).toBe(true);
  });
  it('redirects guests away from the protected shell', () => {
    const auth = TestBed.inject(AuthService); auth.setBearerToken(`${btoa('{}')}.${btoa(JSON.stringify({ sub: '1' }))}.x`);
    expect(runInInjectionContext(TestBed.inject(EnvironmentInjector), () => guestGuard({} as never, {} as never))).toEqual(TestBed.inject(Router).createUrlTree(['/app']));
  });
  it('allows a declared permission and fails closed when permission metadata or grants are missing', () => {
    const auth = TestBed.inject(AuthService); const router = TestBed.inject(Router);
    auth.setBearerToken(`${btoa('{}')}.${btoa(JSON.stringify({ permissions: ['tickets.read'] }))}.x`);
    expect(runInInjectionContext(TestBed.inject(EnvironmentInjector), () => permissionGuard({ data: { permission: 'tickets.read' } } as never, {} as never))).toBe(true);
    expect(runInInjectionContext(TestBed.inject(EnvironmentInjector), () => permissionGuard({ data: { permission: 'tickets.write' } } as never, {} as never))).toEqual(router.createUrlTree(['/unauthorized']));
    expect(runInInjectionContext(TestBed.inject(EnvironmentInjector), () => permissionGuard({ data: {} } as never, {} as never))).toEqual(router.createUrlTree(['/unauthorized']));
  });
});
