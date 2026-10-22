import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), AuthService] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('posts credentials to the regular-auth endpoint and holds the returned token in memory', () => {
    const service = TestBed.inject(AuthService);
    service.login('agent', 'sample-password').subscribe(response => {
      expect(response.tokenType).toBe('Bearer');
      expect(service.isAuthenticated()).toBe(true);
      expect(service.session()?.subject).toBe('42');
    });
    const request = http.expectOne('http://localhost:5018/api/v1/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ loginName: 'agent', password: 'sample-password' });
    request.flush({ accessToken: `${btoa('{}')}.${btoa(JSON.stringify({ sub: '42', exp: Math.floor(Date.now() / 1000) + 300 }))}.signature`, tokenType: 'Bearer', expiresIn: 300, userId: 42, loginName: 'agent', displayName: 'Test Agent' });
  });

  it('treats an expired bearer session as unauthenticated without persistence', () => {
    const service = TestBed.inject(AuthService);
    const header = btoa('{}'); const payload = btoa(JSON.stringify({ sub: '42', exp: Math.floor(Date.now() / 1000) - 1 }));
    service.setBearerToken(`${header}.${payload}.signature`);
    expect(service.isSessionValid()).toBe(false);
  });
  it('reads standard bearer claims without persisting the token', () => {
    const service = TestBed.inject(AuthService);
    const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({ sub: '42', name: 'Test User', roles: ['agent'], permissions: ['tickets.read'] }));
    service.setBearerToken(`${header}.${payload}.signature`);
    expect(service.session()?.subject).toBe('42');
    expect(service.hasPermission('tickets.read')).toBe(true);
    service.logout();
    expect(service.session()).toBeNull();
  });

  it('does not treat role names as permission grants', () => {
    const service = TestBed.inject(AuthService);
    const payload = btoa(JSON.stringify({ sub: '42', roles: ['SUPER_ADMIN'] }));
    service.setBearerToken(`${btoa('{}')}.${payload}.signature`);
    expect(service.hasRole('SUPER_ADMIN')).toBe(true);
    expect(service.hasPermission('TICKET_READ')).toBe(false);
  });
});

