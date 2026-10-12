import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TicketsApiService } from './tickets-api.service';

describe('TicketsApiService', () => {
  let service: TicketsApiService; let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); service = TestBed.inject(TicketsApiService); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('uses the versioned search endpoint and serializes supported filters', () => {
    service.search({ number: 'INC-1', status: 3, createdFrom: '2026-01-01T00:00:00.000Z', page: 2, pageSize: 25 }).subscribe();
    const request = http.expectOne(value => value.url.endsWith('/api/v1/tickets'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('number')).toBe('INC-1'); expect(request.request.params.get('status')).toBe('3');
    expect(request.request.params.get('createdFrom')).toBe('2026-01-01T00:00:00.000Z'); expect(request.request.params.get('page')).toBe('2');
    request.flush({ items: [], page: 2, pageSize: 25, totalCount: 0 });
  });
});
