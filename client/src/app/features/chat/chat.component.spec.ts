import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChatComponent } from './chat.component';
import { FeatureSwitchesService } from '../../core/feature-switches.service';
import { of, throwError } from 'rxjs';

describe('ChatComponent', () => {
  let fixture: ComponentFixture<ChatComponent>;
  const switches = { availability: vi.fn() };

  beforeEach(async () => {
    switches.availability.mockReset().mockReturnValue(of([{ code: 'CHAT', enabled: true }]));
    await TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [{ provide: FeatureSwitchesService, useValue: switches }]
    }).compileComponents();
    fixture = TestBed.createComponent(ChatComponent);
  });

  it('shows a work-item conversation as explicitly unavailable without fabricating history', () => {
    fixture.componentInstance.workItemId = 42;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Work item 42');
    expect(fixture.nativeElement.textContent).toContain('Chat API contract is not available');
    expect(fixture.nativeElement.textContent).toContain('No conversation loaded');
    expect(fixture.nativeElement.querySelectorAll('.chat__message').length).toBe(0);
  });

  it('renders the direct-message surface and prevents unsupported sends and attachments', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.chat').getAttribute('aria-label')).toBe('Direct messages');
    expect(fixture.nativeElement.querySelector('#chat-message').disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('[aria-label="Attach a file"]').disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('[aria-label="Mention someone"]').disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('button[type="submit"]').disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="group"][aria-label="Chat actions unavailable"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('disabled until an approved Chat service is connected');
  });

  it('places initial keyboard focus on the direct-message close control', () => {
    fixture.detectChanges();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[aria-label="Close direct messages"]'));
  });

  it('closes the direct-message drawer on Escape but keeps work-item chat open', () => {
    const closed = vi.fn();
    fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.chat').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(closed).toHaveBeenCalledOnce();

    closed.mockClear();
    fixture.componentInstance.workItemId = 42;
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.chat').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(closed).not.toHaveBeenCalled();
  });

  it('hides the conversation surface when the Chat switch is disabled', () => {
    switches.availability.mockReturnValue(of([{ code: 'CHAT', enabled: false }]));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.chat')).toBeNull();
  });

  it('fails closed when feature availability cannot be loaded', () => {
    switches.availability.mockReturnValue(throwError(() => new Error('offline')));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.chat')).toBeNull();
  });
});
