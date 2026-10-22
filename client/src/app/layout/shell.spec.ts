import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { AuthService } from '../core/auth/auth.service';
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

  beforeEach(async () => {
    auth.logout.mockReset();
    auth.hasPermission.mockReset();
    auth.hasPermission.mockReturnValue(false);
    auth.session.set({ accessToken: 'test', roles: [], permissions: [] });
    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([{ path: 'login', component: LoginStub }]),
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  });

  it('clears the session and immediately returns to sign-in', async () => {
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(TestBed.inject(Router).url).toBe('/login');
  });
});
