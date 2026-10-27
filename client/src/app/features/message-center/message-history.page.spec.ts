import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { FeatureAvailability, FeatureSwitchesService } from '../../core/feature-switches.service';
import { MESSAGE_CENTER_CLIENT, MessageCenterClient, MessageCenterItem } from './message-center.client';
import { MessageHistoryPage } from './message-history.page';

describe('MessageHistoryPage', () => {
  let fixture: ComponentFixture<MessageHistoryPage>;
  let component: MessageHistoryPage;
  const item: MessageCenterItem = { id: 'n-1', title: 'Ticket assigned', summary: 'INC-000123 was assigned to you.', category: 'Assignment', createdAt: '2026-10-27T10:00:00Z', read: false, archived: false, deepLink: '/app/tickets/123' };
  const client = {
    list: vi.fn(), markRead: vi.fn(), archive: vi.fn(), preferences: vi.fn(), savePreferences: vi.fn()
  };
  const availability = { availability: vi.fn<() => Observable<readonly FeatureAvailability[]>>() };

  beforeEach(async () => {
    client.list.mockReset().mockReturnValue(of([item]));
    client.markRead.mockReset().mockReturnValue(of(void 0));
    client.archive.mockReset().mockReturnValue(of(void 0));
    client.preferences.mockReset().mockReturnValue(of({ categories: [] }));
    client.savePreferences.mockReset().mockReturnValue(of(void 0));
    availability.availability.mockReset().mockReturnValue(of([{ code: 'MESSAGE_CENTER', enabled: true }]));
    await TestBed.configureTestingModule({
      imports: [MessageHistoryPage],
      providers: [
        provideRouter([]),
        { provide: MESSAGE_CENTER_CLIENT, useValue: client satisfies MessageCenterClient },
        { provide: FeatureSwitchesService, useValue: availability }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(MessageHistoryPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders saved notifications with accessible search and read-state controls', () => {
    expect(fixture.nativeElement.querySelector('section[aria-labelledby="history-title"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('Notification history');
    expect(fixture.nativeElement.querySelector('label[for="history-search"]').textContent).toContain('Search notifications');
    expect(fixture.nativeElement.querySelector('label[for="history-state"]').textContent).toContain('Read state');
    expect(fixture.nativeElement.textContent).toContain('Ticket assigned');
    expect(fixture.nativeElement.textContent).toContain('Live notification alerts unavailable');
    expect(client.list).toHaveBeenCalledWith({ search: '', state: 'all' });
  });

  it('shows loading and empty states from the existing client contract', () => {
    const pending = new Subject<readonly MessageCenterItem[]>();
    client.list.mockReturnValue(pending);
    component.load(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading notification history');
    pending.next([]); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No notifications match this view');
  });

  it('shows disabled and unavailable states without fabricating notifications', () => {
    client.list.mockClear();
    availability.availability.mockReturnValue(of([{ code: 'MESSAGE_CENTER', enabled: false }]));
    component.checkAvailability(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Message Center is disabled or unavailable');
    expect(client.list).not.toHaveBeenCalled();

    availability.availability.mockReturnValue(throwError(() => new Error('offline')));
    component.checkAvailability(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Notifications remain hidden until availability can be confirmed');
  });

  it('shows API errors and retries through the same client port', () => {
    client.list.mockReturnValue(throwError(() => new Error('offline')));
    component.load(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Notification history could not be loaded');
    client.list.mockReturnValue(of([item]));
    fixture.nativeElement.querySelector('.state--error button').click(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ticket assigned');
  });

  it('passes search and read-state filters to the existing port', () => {
    component.search = 'assignment'; component.searchChanged();
    component.setState('unread');
    expect(client.list).toHaveBeenLastCalledWith({ search: 'assignment', state: 'unread' });
  });

  it('changes read and archive presentation only after a successful client response', () => {
    client.markRead.mockReturnValue(throwError(() => new Error('denied')));
    component.markRead(item); fixture.detectChanges();
    expect(component.items[0].read).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Your view has not been changed');

    client.markRead.mockReturnValue(of(void 0));
    component.markRead(item);
    expect(component.items[0].read).toBe(true);
    component.archive({ ...item, read: true });
    expect(client.archive).toHaveBeenCalledWith('n-1');
    expect(component.items).toEqual([]);
  });

  it('keeps the history unavailable if there is no inbox adapter', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [MessageHistoryPage],
      providers: [provideRouter([]), { provide: MESSAGE_CENTER_CLIENT, useValue: null }, { provide: FeatureSwitchesService, useValue: availability }]
    }).compileComponents();
    const unavailable = TestBed.createComponent(MessageHistoryPage);
    unavailable.detectChanges();
    expect(unavailable.nativeElement.textContent).toContain('no inbox service is connected');
  });
});
