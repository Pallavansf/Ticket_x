import { HttpClient } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../auth/auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
    http = TestBed.inject(HttpClient); controller = TestBed.inject(HttpTestingController); auth = TestBed.inject(AuthService);
    auth.setBearerToken(`${btoa('{}')}.${btoa(JSON.stringify({ sub: '7' }))}.signature`);
  });
  afterEach(() => controller.verify());

  it('adds bearer and correlation headers to API requests', () => {
    http.get('/api/v1/tickets').subscribe();
    const request = controller.expectOne('/api/v1/tickets');
    expect(request.request.headers.get('Authorization')).toBeTruthy();
    expect(request.request.headers.get('Authorization')).toMatch(/^Bearer /);
    expect(request.request.headers.get('X-Correlation-ID')).toMatch(/^[0-9a-f-]{36}$/);
    request.flush({ items: [] });
  });

  it('attaches the token returned by login to subsequent protected requests', () => {
    auth.logout();
    const accessToken = `${btoa('{}')}.${btoa(JSON.stringify({ sub: '42', exp: Math.floor(Date.now() / 1000) + 300, password_change_required: 'true' }))}.signature`;
    auth.login('agent', 'sample-password').subscribe(() => {
      http.get('/api/v1/tickets').subscribe();
    });

    const loginRequest = controller.expectOne('http://localhost:5018/api/v1/auth/login');
    expect(loginRequest.request.headers.has('Authorization')).toBe(false);
    loginRequest.flush({ accessToken, tokenType: 'Bearer', expiresIn: 300, userId: 42, loginName: 'agent', displayName: 'Test Agent' });
    expect(auth.session()?.passwordRotationRequired).toBe(true);

    const protectedRequest = controller.expectOne('/api/v1/tickets');
    expect(protectedRequest.request.headers.get('Authorization')).toBe(`Bearer ${accessToken}`);
    protectedRequest.flush({ items: [] });
  });

  it('clears the session and navigates to login on 401', () => {
    http.get('/api/v1/tickets').subscribe({ error: () => undefined });
    const request = controller.expectOne('/api/v1/tickets');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isAuthenticated()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/');
  });
});
