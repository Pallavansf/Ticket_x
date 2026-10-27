import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FeatureSwitchesService } from './feature-switches.service';

describe('FeatureSwitchesService', () => {
  let service: FeatureSwitchesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(FeatureSwitchesService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('reads persisted switch settings at global and department scopes', () => {
    service.list(12).subscribe();
    const request = http.expectOne(value => value.url.endsWith('/api/v1/admin/feature-switches'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('departmentId')).toBe('12');
    request.flush([]);
  });

  it('uses the authorized update contract and optimistic row version', () => {
    service.set('CHAT', { enabled: false, departmentId: null, expectedRowVersion: 2, reason: 'Maintenance' }).subscribe();
    const request = http.expectOne(value => value.url.endsWith('/api/v1/admin/feature-switches/CHAT'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.expectedRowVersion).toBe(2);
    expect(request.request.body.enabled).toBe(false);
    request.flush({ code: 'CHAT', name: 'Chat', departmentId: null, enabled: false, configured: true, inherited: false, rowVersion: 3 });
  });

  it('fetches caller-scoped availability for navigation hiding', () => {
    service.availability().subscribe();
    const request = http.expectOne(value => value.url.endsWith('/api/v1/features/availability'));
    expect(request.request.method).toBe('GET');
    request.flush([{ code: 'CHAT', enabled: false }]);
  });
});
