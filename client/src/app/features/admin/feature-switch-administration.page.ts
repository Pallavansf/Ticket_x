import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { FeatureSwitchesService, FeatureSwitchView } from '../../core/feature-switches.service';

@Component({ standalone: true, imports: [FormsModule], templateUrl: './feature-switch-administration.page.html', styleUrl: './feature-switch-administration.page.scss' })
export class FeatureSwitchAdministrationPage {
  private readonly service = inject(FeatureSwitchesService);
  readonly rows = signal<readonly FeatureSwitchView[]>([]);
  readonly loading = signal(false);
  readonly saving = signal<string | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  scope: 'GLOBAL' | 'DEPARTMENT' = 'GLOBAL';
  departmentId = '';
  reason = '';

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const departmentId = this.scope === 'DEPARTMENT' ? Number(this.departmentId) : null;
      if (departmentId !== null && (!Number.isSafeInteger(departmentId) || departmentId <= 0)) {
        this.rows.set([]); this.error.set('Enter a valid department ID to load department settings.'); return;
      }
      this.rows.set(await firstValueFrom(this.service.list(departmentId)));
    } catch (error) { this.error.set(this.errorMessage(error)); }
    finally { this.loading.set(false); }
  }

  async setEnabled(row: FeatureSwitchView, enabled: boolean): Promise<void> {
    if (this.saving()) return;
    const departmentId = this.scope === 'DEPARTMENT' ? Number(this.departmentId) : null;
    if (departmentId !== null && (!Number.isSafeInteger(departmentId) || departmentId <= 0)) {
      this.error.set('Enter a valid department ID before changing a setting.'); return;
    }
    this.saving.set(row.code); this.error.set(''); this.notice.set('');
    try {
      const updated = await firstValueFrom(this.service.set(row.code, {
        enabled, departmentId, expectedRowVersion: row.rowVersion, reason: this.reason.trim() || null
      }));
      this.rows.update(rows => rows.map(value => value.code === row.code ? updated : value));
      this.notice.set(`${updated.name} ${updated.enabled ? 'enabled' : 'disabled'} for ${this.scope === 'GLOBAL' ? 'all departments by default' : `department ${departmentId}`}.`);
      this.reason = '';
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally { this.saving.set(null); }
  }

  scopeChanged(): void { this.notice.set(''); void this.load(); }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 400) return 'The switch value or scope was invalid. Review the settings and try again.';
      if (error.status === 403) return 'You are not authorized to administer feature switches.';
      if (error.status === 404) return 'The requested switch or department was not found.';
      if (error.status === 409) return 'The setting changed or the configuration service rejected the update. Reload and review the current value.';
      if (error.status === 0 || error.status >= 500) return 'Feature settings are unavailable. No successful change was confirmed.';
    }
    return 'Feature settings could not be loaded or saved.';
  }
}
