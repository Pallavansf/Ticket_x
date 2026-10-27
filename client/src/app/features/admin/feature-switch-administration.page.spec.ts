import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { FeatureSwitchesService, FeatureSwitchView } from '../../core/feature-switches.service';
import { FeatureSwitchAdministrationPage } from './feature-switch-administration.page';

describe('FeatureSwitchAdministrationPage', () => {
  let fixture: ComponentFixture<FeatureSwitchAdministrationPage>;
  const row: FeatureSwitchView = { code: 'CHAT', name: 'Chat', departmentId: null, enabled: false, configured: false, inherited: false, rowVersion: 0 };
  const service = { list: vi.fn(), set: vi.fn() };

  beforeEach(async () => {
    service.list.mockReset().mockReturnValue(of([row]));
    service.set.mockReset().mockReturnValue(of({ ...row, enabled: true, configured: true, rowVersion: 1 }));
    await TestBed.configureTestingModule({
      imports: [FeatureSwitchAdministrationPage],
      providers: [{ provide: FeatureSwitchesService, useValue: service }]
    }).compileComponents();
    fixture = TestBed.createComponent(FeatureSwitchAdministrationPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads persisted rows and shows missing configuration as disabled', () => {
    expect(service.list).toHaveBeenCalledWith(null);
    expect(fixture.nativeElement.textContent).toContain('Missing; fails closed');
    expect(fixture.nativeElement.querySelector('input[type="checkbox"]').checked).toBe(false);
  });

  it('reports success only after the API confirms the update', async () => {
    fixture.componentInstance.setEnabled(row, true);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(service.set).toHaveBeenCalledWith('CHAT', expect.objectContaining({ enabled: true, expectedRowVersion: 0 }));
    expect(fixture.nativeElement.textContent).toContain('Chat enabled');
  });

  it('keeps the failed update visible as an error and does not report success', async () => {
    service.set.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 403 })));
    fixture.componentInstance.setEnabled(row, true);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('not authorized');
    expect(fixture.nativeElement.textContent).not.toContain('Chat enabled');
  });

  it('requires a positive department identifier for scoped edits', async () => {
    fixture.componentInstance.scope = 'DEPARTMENT';
    fixture.componentInstance.departmentId = '0';
    await fixture.componentInstance.setEnabled(row, true);
    expect(service.set).not.toHaveBeenCalled();
    expect(fixture.componentInstance.error()).toContain('valid department ID');
  });

  it('shows configuration outages without presenting an empty successful state', async () => {
    service.list.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 503 })));
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Feature settings are unavailable');
    expect(fixture.nativeElement.textContent).not.toContain('No feature switches were returned');
  });
});
