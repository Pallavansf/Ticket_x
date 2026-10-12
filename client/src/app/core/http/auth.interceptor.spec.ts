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

  it('clears the session and navigates to login on 401', () => {
    http.get('/api/v1/tickets').subscribe({ error: () => undefined });
    const request = controller.expectOne('/api/v1/tickets');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isAuthenticated()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/');
  });
});
