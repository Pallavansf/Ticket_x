import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { TicketListPage } from './ticket-list.page';
import { TicketsApiService } from '../../core/tickets-api.service';

describe('TicketListPage', () => {
  let fixture: ComponentFixture<TicketListPage>; let component: TicketListPage;
  const api = { search: vi.fn() };
  beforeEach(async () => { api.search.mockReset(); api.search.mockReturnValue(of({ items: [], page: 1, pageSize: 25, totalCount: 0 })); await TestBed.configureTestingModule({ imports: [TicketListPage], providers: [provideRouter([]), { provide: TicketsApiService, useValue: api }] }).compileComponents(); fixture = TestBed.createComponent(TicketListPage); component = fixture.componentInstance; fixture.detectChanges(); });
  it('renders an empty result without fabricating ticket rows', () => { expect(component.result?.items.length).toBe(0); expect(fixture.nativeElement.textContent).toContain('No tickets found'); });
  it('renders API data and sends filters and pagination', () => { api.search.mockReturnValue(of({ items: [{ id: 1, number: 'INC-1', subject: 'Printer', type: 0, status: 0, priorityId: null, currentDepartmentId: null, currentTeamId: null, currentAssignee: null, workItems: [] }], page: 1, pageSize: 25, totalCount: 1 })); component.filters.patchValue({ number: 'INC-1' }); component.submit(); expect(api.search).toHaveBeenLastCalledWith(expect.objectContaining({ number: 'INC-1', page: 1, pageSize: 25 })); expect(component.result?.items[0].number).toBe('INC-1'); });
  it('maps authorization, server, and network failures to explicit states', () => { api.search.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 }))); component.load(); expect(component.error).toBe('forbidden'); api.search.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 }))); component.load(); expect(component.error).toBe('server'); api.search.mockReturnValue(throwError(() => new Error('offline'))); component.load(); expect(component.error).toBe('network'); });
});


