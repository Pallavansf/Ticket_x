import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { TicketsApiService } from '../../core/tickets-api.service';
import { TicketDetailPage } from './ticket-detail.page';

describe('TicketDetailPage', () => {
  let fixture: ComponentFixture<TicketDetailPage>;
  let component: TicketDetailPage;
  const api = { get: vi.fn(), completeWorkItem: vi.fn() };
  const auth = { hasPermission: vi.fn(() => true) };
  const ticket = {
    id: 42,
    number: 'INC-42',
    type: 0,
    status: 3,
    subject: 'Printer',
    priorityId: null,
    currentDepartmentId: null,
    currentTeamId: null,
    currentAssignee: null,
    serviceId: 10,
    categoryId: 20,
    requestTypeId: 30,
    history: [{ id: 1, action: 'TICKET_CREATED', actorId: 7, occurredAt: '2026-10-23T00:00:00Z', oldValue: null, newValue: 'New', reason: null, category: 'LIFECYCLE' }],
    workItems: [{
      id: 9,
      number: 'WI-9',
      publicId: 'PRVT-9F2C1AB8D3E4',
      status: 'Waiting',
      assignedToUserId: 7,
      waitingMinutes: 12
    }]
  };

  beforeEach(async () => {
    api.get.mockReset();
    api.get.mockReturnValue(of(ticket));
    api.completeWorkItem.mockReset();
    auth.hasPermission.mockReset();
    auth.hasPermission.mockReturnValue(true);
    await TestBed.configureTestingModule({
      imports: [TicketDetailPage],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: TicketsApiService, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: '42' }),
              queryParamMap: convertToParamMap({ number: 'INC-42', status: '3', type: '2', page: '2' })
            }
          }
        }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(TicketDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads and renders authorized DTO fields and work items', () => {
    expect(api.get).toHaveBeenCalledWith(42);
    expect(fixture.nativeElement.textContent).toContain('INC-42');
    expect(fixture.nativeElement.textContent).toContain('WI-9');
    expect(fixture.nativeElement.textContent).toContain('TICKET_CREATED');
  });

  it('preserves the list filter context when returning to ticket search', () => {
    expect(component.returnQueryParams).toEqual({
      number: 'INC-42', status: '3', type: '2', page: '2'
    });
  });

  it('confirms completion, sends the public work-item id, and refreshes state', () => {
    api.completeWorkItem.mockReturnValue(of({ ok: true }));

    component.requestCompletion(ticket.workItems[0].publicId);
    component.confirmCompletion();

    expect(api.completeWorkItem).toHaveBeenCalledWith('PRVT-9F2C1AB8D3E4', expect.any(String));
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('keeps completion success distinct from a refresh failure', () => {
    api.completeWorkItem.mockReturnValue(of({ ok: true }));
    api.get.mockReturnValueOnce(throwError(() => new Error('refresh offline')));

    component.requestCompletion(ticket.workItems[0].publicId);
    component.confirmCompletion();

    expect(component.refreshError).toBe('network');
    expect(component.actionError).toBeNull();
  });

  it('maps not-found, forbidden, and network errors', () => {
    api.get.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    api.completeWorkItem.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    component.requestCompletion(ticket.workItems[0].publicId);
    component.confirmCompletion();
    expect(component.actionError).toBe('conflict');

    api.get.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    component.ngOnInit();
    expect(component.error).toBe('not-found');

    api.get.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    component.ngOnInit();
    expect(component.error).toBe('forbidden');

    api.get.mockReturnValue(throwError(() => new Error('offline')));
    component.ngOnInit();
    expect(component.error).toBe('network');
  });

  it('does not offer completion when the permission is absent', () => {
    auth.hasPermission.mockReturnValue(false);

    component.requestCompletion(ticket.workItems[0].publicId);
    component.confirmCompletion();

    expect(component.pendingCompletion).toBeNull();
    expect(api.completeWorkItem).not.toHaveBeenCalled();
  });
});
