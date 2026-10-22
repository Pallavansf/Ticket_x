import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LoginPage } from './login';
import { AuthService } from '../../core/auth/auth.service';

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let component: LoginPage;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LoginPage], providers: [provideRouter([{ path: 'login', component: LoginPage }]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    fixture = TestBed.createComponent(LoginPage); component = fixture.componentInstance; fixture.detectChanges();
  });
  it('rejects invalid input before submitting', () => {
    component.submit();
    expect(component.form.controls.identifier.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
    expect(component.loading).toBe(false);
  });
  it('renders an authentication error after a rejected login response', async () => {
    component.form.setValue({ identifier: 'user@example.com', password: 'password123' });
    component.submit();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('http://localhost:5018/api/v1/auth/login');
    request.flush({ title: 'Invalid login name or password.' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable(); fixture.detectChanges();
    expect(component.loading).toBe(false);
    expect(component.errorMessage).toContain('Sign-in failed');
    http.verify();
  });
  it('requires password rotation and clears the session after a successful change', async () => {
    const http = TestBed.inject(HttpTestingController);
    const accessToken = `${btoa('{}')}.${btoa(JSON.stringify({ sub: '42', exp: Math.floor(Date.now() / 1000) + 300, password_change_required: 'true' }))}.signature`;
    component.form.setValue({ identifier: 'admin@example.com', password: 'temporary-password' });
    component.submit();
    http.expectOne('http://localhost:5018/api/v1/auth/login').flush({
      accessToken, tokenType: 'Bearer', expiresIn: 300, userId: 42,
      loginName: 'admin@example.com', displayName: 'Initial Administrator', passwordRotationRequired: true
    });
    await fixture.whenStable();
    expect(component.requiresPasswordRotation).toBe(true);

    component.rotationForm.setValue({ newPassword: 'long-new-password-2025', confirmPassword: 'long-new-password-2025' });
    component.changePassword();
    const changeRequest = http.expectOne('http://localhost:5018/api/v1/auth/change-password');
    expect(changeRequest.request.method).toBe('POST');
    expect(changeRequest.request.body).toEqual({
      currentPassword: 'temporary-password',
      newPassword: 'long-new-password-2025',
      confirmPassword: 'long-new-password-2025'
    });
    changeRequest.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    expect(component.loading).toBe(false);
    expect(component.errorMessage).toBeNull();
    expect(TestBed.inject(AuthService).session()).toBeNull();
    http.verify();
  });
  it('does not submit a mismatched password confirmation', () => {
    component.rotationForm.setValue({ newPassword: 'long-new-password-2025', confirmPassword: 'different-new-password' });
    component.changePassword();
    expect(component.errorMessage).toContain('do not match');
    expect(component.loading).toBe(false);
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5018/api/v1/auth/change-password');
  });
  it('rejects external return URLs', () => {
    expect(component.safeReturnUrl('https://evil.example')).toBe('/app');
    expect(component.safeReturnUrl('//evil.example')).toBe('/app');
    expect(component.safeReturnUrl('/app/reports')).toBe('/app/reports');
  });
});
