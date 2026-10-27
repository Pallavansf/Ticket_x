import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { AuthService } from '../core/auth/auth.service';
import { FeatureSwitchesService } from '../core/feature-switches.service';
import { RealtimeService } from '../core/realtime.service';
import { of, throwError } from 'rxjs';
import { ShellComponent } from './shell';

@Component({ standalone: true, template: '' })
class LoginStub {}

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;
  const auth = {
    session: signal({ accessToken: 'test', roles: [], permissions: [] }),
    logout: vi.fn(),
    hasPermission: vi.fn(() => false)
  };
  const featureSwitches = { availability: vi.fn() };
  const realtime = { state: signal('disconnected'), start: vi.fn(), stop: vi.fn() };

  beforeEach(async () => {
    auth.logout.mockReset();
    auth.hasPermission.mockReset();
    auth.hasPermission.mockReturnValue(false);
    auth.session.set({ accessToken: 'test', roles: [], permissions: [] });
    featureSwitches.availability.mockReset().mockReturnValue(of([{ code: 'CHAT', enabled: true }, { code: 'MESSAGE_CENTER', enabled: true }]));
    realtime.start.mockReset().mockResolvedValue(undefined);
    realtime.stop.mockReset().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([{ path: 'login', component: LoginStub }]),
        { provide: AuthService, useValue: auth },
        { provide: FeatureSwitchesService, useValue: featureSwitches },
        { provide: RealtimeService, useValue: realtime }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  });

  it('clears the session and immediately returns to sign-in', async () => {
    fixture.nativeElement.querySelector('.shell__account button').click();
    await fixture.whenStable();
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(realtime.stop).toHaveBeenCalledOnce();
    expect(TestBed.inject(Router).url).toBe('/login');
  });

  it('opens the direct-message drawer, focuses its close control, and restores trigger focus on close', () => {
    const trigger = fixture.nativeElement.querySelector('.shell__chat') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#direct-chat-drawer')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Chat API contract is not available');
    expect(document.activeElement?.getAttribute('aria-label')).toBe('Close direct messages');
    fixture.nativeElement.querySelector('[aria-label="Close direct messages"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#direct-chat-drawer')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('closes the direct-message drawer on Escape and returns focus to its trigger', () => {
    const trigger = fixture.nativeElement.querySelector('.shell__chat') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();
    fixture.nativeElement.querySelector('#direct-chat-drawer .chat').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#direct-chat-drawer')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('hides Chat and Message Center navigation when switch state is disabled or unavailable', () => {
    featureSwitches.availability.mockReturnValue(of([{ code: 'CHAT', enabled: false }, { code: 'MESSAGE_CENTER', enabled: false }]));
    fixture.componentInstance.ngOnInit();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.shell__chat')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-message-center')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/app/messages"]')).toBeNull();
  });

  it('fails closed and hides feature navigation when availability cannot be loaded', () => {
    featureSwitches.availability.mockReturnValue(throwError(() => new Error('offline')));
    fixture.componentInstance.ngOnInit();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.shell__chat')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-message-center')).toBeNull();
  });

  it('shows live-update availability and starts transport only when its feature switch is enabled', () => {
    expect(fixture.nativeElement.querySelector('.shell__realtime').textContent).toContain('unavailable');
    expect(realtime.start).not.toHaveBeenCalled();

    featureSwitches.availability.mockReturnValue(of([{ code: 'REALTIME', enabled: true }]));
    fixture.componentInstance.ngOnInit();
    fixture.detectChanges();
    expect(realtime.start).toHaveBeenCalledOnce();
  });
});
