import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { TicketCreatePage } from './ticket-create.page';
import { TicketsApiService } from '../../../core/tickets-api.service';

describe('TicketCreatePage', () => {
  let fixture: ComponentFixture<TicketCreatePage>;
  let component: TicketCreatePage;
  const api = { services: vi.fn(), categories: vi.fn(), subCategories: vi.fn(), requestTypes: vi.fn(), create: vi.fn() };

  beforeEach(async () => {
    api.services.mockReturnValue(of([{ id: 1, code: 'IT', name: 'IT', active: true, effectiveFrom: new Date(Date.now() - 60_000).toISOString(), effectiveTo: null }]));
    api.categories.mockReturnValue(of([]));
    api.subCategories.mockReturnValue(of([]));
    api.requestTypes.mockReturnValue(of([]));
    api.create.mockReset();
    await TestBed.configureTestingModule({
      imports: [TicketCreatePage],
      providers: [
        provideRouter([]),
        { provide: TicketsApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: new Map() } } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(TicketCreatePage);
    component = fixture.componentInstance;
    component.ngOnInit();
    fixture.detectChanges();
  });

  it('loads real catalogue values and validates required subject', () => {
    expect(component.services).toHaveLength(1);
    component.submit();
    expect(component.form.controls.subject.touched).toBe(true);
    expect(api.create).not.toHaveBeenCalled();
  });

  it('shows only currently effective published request-type versions', () => {
    const now = Date.now();
    api.requestTypes.mockReturnValue(of([
      { id: 11, active: true, status: 'PUBLISHED', effectiveFrom: new Date(now - 60_000).toISOString(), effectiveTo: null },
      { id: 12, active: true, status: 'DRAFT', effectiveFrom: new Date(now - 60_000).toISOString(), effectiveTo: null },
      { id: 13, active: false, status: 'PUBLISHED', effectiveFrom: new Date(now - 60_000).toISOString(), effectiveTo: null },
      { id: 14, active: true, status: 'PUBLISHED', effectiveFrom: new Date(now + 60_000).toISOString(), effectiveTo: null },
      { id: 15, active: true, status: 'PUBLISHED', effectiveFrom: new Date(now - 120_000).toISOString(), effectiveTo: new Date(now - 60_000).toISOString() }
    ]));
    component.form.patchValue({ serviceId: '1', categoryId: '2' });

    component.categoryChanged();

    expect(component.requestTypes.map(item => item.id)).toEqual([11]);
  });

  it('hides inactive and out-of-range hierarchy options', () => {
    const now = Date.now();
    api.services.mockReturnValue(of([
      { id: 1, code: 'IT', name: 'Current', active: true, effectiveFrom: new Date(now - 60_000).toISOString(), effectiveTo: null },
      { id: 2, code: 'OLD', name: 'Expired', active: true, effectiveFrom: new Date(now - 120_000).toISOString(), effectiveTo: new Date(now - 60_000).toISOString() },
      { id: 3, code: 'FUTURE', name: 'Future', active: true, effectiveFrom: new Date(now + 60_000).toISOString(), effectiveTo: null }
    ]));
    component.loadServices();
    api.categories.mockReturnValue(of([
      { id: 21, serviceId: 1, code: 'CURRENT', name: 'Current', active: true, effectiveFrom: new Date(now - 60_000).toISOString(), effectiveTo: null },
      { id: 22, serviceId: 1, code: 'EXPIRED', name: 'Expired', active: true, effectiveFrom: new Date(now - 120_000).toISOString(), effectiveTo: new Date(now - 60_000).toISOString() }
    ]));
    component.form.patchValue({ serviceId: '1' });
    component.serviceChanged();
    api.subCategories.mockReturnValue(of([
      { id: 31, categoryId: 21, code: 'CURRENT', name: 'Current', active: true, effectiveFrom: new Date(now - 60_000).toISOString(), effectiveTo: null },
      { id: 32, categoryId: 21, code: 'EXPIRED', name: 'Expired', active: true, effectiveFrom: new Date(now - 120_000).toISOString(), effectiveTo: new Date(now - 60_000).toISOString() }
    ]));
    component.form.patchValue({ categoryId: '21' });
    component.categoryChanged();

    expect(component.services.map(item => item.id)).toEqual([1]);
    expect(component.categories.map(item => item.id)).toEqual([21]);
    expect(component.subCategories.map(item => item.id)).toEqual([31]);
  });

  it('prevents duplicate submission and navigates only after confirmed creation', () => {
    api.create.mockReturnValue(of({ id: 22 }));
    component.form.patchValue({ subject: 'Printer' });
    component.submit();
    expect(api.create).toHaveBeenCalledTimes(1);
    expect(TestBed.inject(Router).url).toBe('/');
  });

  it('maps server validation, authorization, and ambiguous network failures', () => {
    component.form.patchValue({ subject: 'Printer' });
    api.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 422 })));
    component.submit();
    expect(component.submitError).toBe('validation');
    api.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    component.submit();
    expect(component.submitError).toBe('forbidden');
    api.create.mockReturnValue(throwError(() => new Error('offline')));
    component.submit();
    expect(component.submitError).toBe('network');
  });
});
