import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface User { id: number; userCode: string; loginName: string; displayName: string; email: string | null; departmentId: number | null; teamId: number | null; active: boolean; phone: string | null; managerUserId: number | null; emailVerified: boolean; }
interface Department { id: number; code: string; name: string; active: boolean; }
interface Team { id: number; departmentId: number; code: string; name: string; active: boolean; }

@Component({ standalone: true, imports: [FormsModule, RouterLink], templateUrl: './user-administration.page.html', styleUrl: './user-administration.page.scss' })
export class UserAdministrationPage {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl + '/api/v1/admin';
  readonly users = signal<User[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly teams = signal<Team[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly selectedUser = computed(() => this.users().find(user => user.id === this.selectedId()) ?? null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  createUserCode = ''; createLoginName = ''; createDisplayName = ''; createEmail = '';
  createDepartmentId: number | null = null; createTeamId: number | null = null; createTeamOptions: Team[] = [];
  editDisplayName = ''; editEmail = ''; editPhone = ''; newPassword = '';

  constructor() { void this.load(); }
  async load(): Promise<void> {
    this.loading.set(true); this.error.set('');
    try {
      const [users, departments, teams] = await Promise.all([
        firstValueFrom(this.http.get<User[]>(this.api + '/users')),
        firstValueFrom(this.http.get<Department[]>(this.api + '/departments')),
        firstValueFrom(this.http.get<Team[]>(this.api + '/teams'))
      ]);
      this.users.set(users); this.departments.set(departments); this.teams.set(teams);
      if (!this.selectedId() && users.length) this.selectUser(users[0]);
    } catch (error) { this.error.set(this.errorMessage(error)); }
    finally { this.loading.set(false); }
  }
  selectUser(user: User): void {
    this.selectedId.set(user.id); this.editDisplayName = user.displayName;
    this.editEmail = user.email ?? ''; this.editPhone = user.phone ?? ''; this.newPassword = '';
  }
  setCreateDepartment(value: string): void {
    this.createDepartmentId = value ? Number(value) : null; this.createTeamId = null;
    this.createTeamOptions = this.teams().filter(team => team.departmentId === this.createDepartmentId && team.active);
  }
  async createUser(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true); this.error.set(''); this.notice.set('');
    try {
      const user = await firstValueFrom(this.http.post<User>(this.api + '/users', {
        userCode: this.createUserCode.trim(), loginName: this.createLoginName.trim(),
        displayName: this.createDisplayName.trim(), email: this.createEmail.trim() || null,
        departmentId: this.createDepartmentId, teamId: this.createTeamId
      }));
      this.users.update(users => [...users, user].sort((a, b) => a.loginName.localeCompare(b.loginName)));
      this.selectUser(user); this.createUserCode = ''; this.createLoginName = '';
      this.createDisplayName = ''; this.createEmail = ''; this.createDepartmentId = null;
      this.createTeamId = null; this.createTeamOptions = [];
      this.notice.set('User created. Set a temporary password below and assign a role from Role administration.');
    } catch (error) { this.error.set(this.errorMessage(error)); }
    finally { this.saving.set(false); }
  }
  async saveProfile(): Promise<void> {
    const user = this.selectedUser(); if (!user || this.saving()) return;
    this.saving.set(true); this.error.set(''); this.notice.set('');
    try {
      const updated = await firstValueFrom(this.http.patch<User>(this.api + '/users/' + user.id + '/profile', {
        displayName: this.editDisplayName.trim(), email: this.editEmail.trim() || null,
        phone: this.editPhone.trim() || null
      }));
      this.replaceUser(updated); this.notice.set('Profile saved.');
    } catch (error) { this.error.set(this.errorMessage(error)); }
    finally { this.saving.set(false); }
  }
  async setActive(user: User, active: boolean): Promise<void> {
    const verb = active ? 'activate' : 'deactivate';
    if (!window.confirm('Are you sure you want to ' + verb + ' ' + user.displayName + '?')) return;
    this.saving.set(true); this.error.set(''); this.notice.set('');
    try {
      const updated = await firstValueFrom(this.http.post<User>(this.api + '/users/' + user.id + '/active', { active }));
      this.replaceUser(updated); this.notice.set('Account ' + (active ? 'activated.' : 'deactivated.'));
    } catch (error) { this.error.set(this.errorMessage(error)); }
    finally { this.saving.set(false); }
  }
  async setPassword(): Promise<void> {
    const user = this.selectedUser(); if (!user || this.saving() || this.newPassword.length < 12) return;
    if (!window.confirm('Set a temporary password for ' + user.displayName + '? They must change it at next sign in.')) return;
    this.saving.set(true); this.error.set(''); this.notice.set('');
    try {
      await firstValueFrom(this.http.post<void>(this.api + '/users/' + user.id + '/password', { password: this.newPassword }));
      this.newPassword = ''; this.notice.set('Temporary password saved. The user must change it at next sign in.');
    } catch (error) { this.error.set(this.errorMessage(error)); }
    finally { this.saving.set(false); }
  }
  teamsFor(departmentId: number | null): Team[] { return this.teams().filter(x => x.departmentId === departmentId && x.active); }
  departmentName(id: number | null): string { return this.departments().find(x => x.id === id)?.name ?? 'Unassigned'; }
  teamName(id: number | null): string { return this.teams().find(x => x.id === id)?.name ?? 'Unassigned'; }
  private replaceUser(user: User): void { this.users.update(users => users.map(x => x.id === user.id ? user : x)); this.selectUser(user); }
  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'The administration service could not be reached.';
      if (error.status === 403) return 'You are not authorized to manage these users.';
      if (error.status === 401) return 'Your session has expired. Sign in again.';
      return error.error?.title ?? 'The user change could not be saved.';
    }
    return 'The user administration page could not be loaded.';
  }
}