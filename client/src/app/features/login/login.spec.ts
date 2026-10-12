import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LoginPage } from './login';

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let component: LoginPage;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LoginPage], providers: [provideRouter([])] }).compileComponents();
    fixture = TestBed.createComponent(LoginPage); component = fixture.componentInstance; fixture.detectChanges();
  });
  it('rejects invalid input before submitting', () => {
    component.submit();
    expect(component.form.controls.identifier.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
    expect(component.loading).toBe(false);
  });
  it('renders the backend integration error for valid submission', async () => {
    component.form.setValue({ identifier: 'user@example.com', password: 'password123' });
    component.submit();
    await fixture.whenStable(); fixture.detectChanges();
    expect(component.loading).toBe(false);
    expect(component.errorMessage).toContain('does not expose a login endpoint');
  });
  it('rejects external return URLs', () => {
    expect(component.safeReturnUrl('https://evil.example')).toBe('/app');
    expect(component.safeReturnUrl('//evil.example')).toBe('/app');
    expect(component.safeReturnUrl('/app/reports')).toBe('/app/reports');
  });
});
