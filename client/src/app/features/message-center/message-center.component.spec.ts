import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { MESSAGE_CENTER_CLIENT, MessageCenterClient, MessageCenterItem } from './message-center.client';
import { MessageCenterComponent } from './message-center.component';

describe('MessageCenterComponent', () => {
  let fixture: ComponentFixture<MessageCenterComponent>;
  let component: MessageCenterComponent;
  const item: MessageCenterItem = { id: 'n-1', title: 'Ticket assigned', summary: 'INC-000123 was assigned to you.', category: 'Assignment', createdAt: '2026-10-27T10:00:00Z', read: false, archived: false, deepLink: '/app/tickets/123' };
  const client = {
    list: vi.fn(), markRead: vi.fn(), archive: vi.fn(), preferences: vi.fn(), savePreferences: vi.fn()
  };

  beforeEach(async () => {
    client.list.mockReset().mockReturnValue(of([item]));
    client.markRead.mockReset().mockReturnValue(of(void 0));
    client.archive.mockReset().mockReturnValue(of(void 0));
    client.preferences.mockReset().mockReturnValue(of({ categories: [{ code: 'APPROVAL', label: 'Approval requests', enabled: true, mandatory: true }, { code: 'COMMENT', label: 'Comments', enabled: true, mandatory: false }] }));
    client.savePreferences.mockReset().mockReturnValue(of(void 0));
    await TestBed.configureTestingModule({
      imports: [MessageCenterComponent],
      providers: [provideRouter([]), { provide: MESSAGE_CENTER_CLIENT, useValue: client satisfies MessageCenterClient }]
    }).compileComponents();
    fixture = TestBed.createComponent(MessageCenterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads recent messages when opened and shows the unread count', () => {
    fixture.nativeElement.querySelector('.bell').click();
    fixture.detectChanges();
    expect(client.list).toHaveBeenCalledWith({ search: '', state: 'all' });
    expect(fixture.nativeElement.querySelector('.badge').textContent.trim()).toBe('1');
    expect(fixture.nativeElement.textContent).toContain('Ticket assigned');
  });

  it('shows empty, loading and unavailable states without fabricating messages', () => {
    client.list.mockReturnValue(of([]));
    component.toggle(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No messages match this view');
    client.list.mockReturnValue(throwError(() => new Error('offline')));
    component.load(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('inbox service is not connected or could not be reached');
    const loadingFixture = TestBed.createComponent(MessageCenterComponent);
    loadingFixture.componentInstance.open = true;
    loadingFixture.componentInstance.loading = true;
    loadingFixture.detectChanges();
    expect(loadingFixture.nativeElement.textContent).toContain('Loading messages');
  });

  it('marks read and archives through the client before updating the view', () => {
    component.items = [item];
    component.markRead(item);
    expect(client.markRead).toHaveBeenCalledWith('n-1');
    expect(component.items[0].read).toBe(true);
    component.archive(component.items[0]);
    expect(client.archive).toHaveBeenCalledWith('n-1');
    expect(component.items).toEqual([]);
  });

  it('applies search and read-state filters to the client request', () => {
    component.toggle();
    component.search = 'approval'; component.searchChanged();
    component.setState('unread');
    expect(client.list).toHaveBeenLastCalledWith({ search: 'approval', state: 'unread' });
    expect(component.unreadCount).toBe(1);
  });

  it('loads preferences, locks mandatory categories and saves optional choices', () => {
    component.open = true;
    component.showPreferences(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Approval requests');
    expect(fixture.nativeElement.querySelector('.preferences input').disabled).toBe(true);
    component.setPreference(1, false);
    component.savePreferences();
    expect(client.savePreferences).toHaveBeenCalledWith({ categories: [
      { code: 'APPROVAL', label: 'Approval requests', enabled: true, mandatory: true },
      { code: 'COMMENT', label: 'Comments', enabled: false, mandatory: false }
    ] });
  });

  it('keeps the no-backend state explicit when no client adapter is registered', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [MessageCenterComponent], providers: [provideRouter([]), { provide: MESSAGE_CENTER_CLIENT, useValue: null }] });
    const unavailable = TestBed.createComponent(MessageCenterComponent);
    unavailable.componentInstance.toggle(); unavailable.detectChanges();
    expect(unavailable.nativeElement.textContent).toContain('inbox service is not connected');
  });
});
