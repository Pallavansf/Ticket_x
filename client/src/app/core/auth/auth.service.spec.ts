import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthIntegrationGapError, AuthService } from './auth.service';

describe('AuthService', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [AuthService] }));

  it('reports the real backend login integration gap without sending credentials', async () => {
    const service = TestBed.inject(AuthService);
    await expect(firstValueFrom(service.login('user@example.com', 'password123'))).rejects.toBeInstanceOf(AuthIntegrationGapError);
    expect(service.isAuthenticated()).toBe(false);
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
});



