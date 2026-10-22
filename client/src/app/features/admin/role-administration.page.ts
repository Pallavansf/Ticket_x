import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface Role {
  id: number;
  code: string;
  name: string;
  description: string | null;
  systemRole: boolean;
  active: boolean;
}

interface Permission {
  id: number;
  code: string;
  name: string;
  description: string | null;
  moduleCode: string;
  actionCode: string;
  scopeClass: string;
  securityClass: string;
  readOnly: boolean;
  active: boolean;
}

interface RolePermission {
  roleId: number;
  permissionId: number;
  validFrom: string;
  validTo: string | null;
  active: boolean;
}

interface Membership {
  id: number;
  userId: number;
  roleId: number;
  validFrom: string;
  validTo: string | null;
  active: boolean;
}

interface RoleScope {
  id: number;
  userRoleId: number;
  scopeType: string;
  organizationId: number | null;
  departmentId: number | null;
  teamId: number | null;
  clientId: number | null;
  resourceType: string | null;
  resourceId: string | null;
  validFrom: string;
  validTo: string | null;
  active: boolean;
}

interface User {
  id: number;
  loginName: string;
  displayName: string;
  departmentId: number | null;
  teamId: number | null;
  active: boolean;
}

interface Department {
  id: number;
  name: string;
  active: boolean;
}

interface Team {
  id: number;
  departmentId: number;
  name: string;
  active: boolean;
}

interface AdminHeadDelegation {
  id: number;
  adminHeadUserId: number;
  adminUserId: number;
  validFrom: string;
  validTo: string | null;
  active: boolean;
}

interface RbacProposal {
  id: string;
  operationType: string;
  status: string;
  targetRoleId: number;
  targetRoleCode: string;
  targetPermissionId: number;
  targetPermissionCode: string;
  targetPermissionName: string;
  moduleCode: string;
  actionCode: string;
  scopeClass: string;
  securityClass: string;
  initiatorUserId: number;
  createdAt: string;
  expiresAt: string;
  reason: string;
  approverUserId: number | null;
  cancelledByUserId: number | null;
  decisionAt: string | null;
  decisionReason: string | null;
  applicationOutcome: string | null;
  correlationId: string;
  rowVersion: number;
  canApprove: boolean;
  canReject: boolean;
  canCancel: boolean;
}

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './role-administration.page.html',
  styleUrl: './role-administration.page.scss'
})
export class RoleAdministrationPage {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiBaseUrl}/api/v1/admin`;

  readonly roles = signal<Role[]>([]);
  readonly permissions = signal<Permission[]>([]);
  readonly selectedRoleId = signal<number | null>(null);
  readonly selectedPermissionIds = signal<ReadonlySet<number>>(new Set());
  readonly savedPermissionIds = signal<ReadonlySet<number>>(new Set());
  readonly rolePermissions = signal<RolePermission[]>([]);
  readonly users = signal<User[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly teams = signal<Team[]>([]);
  readonly memberships = signal<Membership[]>([]);
  readonly scopes = signal<RoleScope[]>([]);
  readonly delegations = signal<AdminHeadDelegation[]>([]);
  readonly delegationError = signal('');
  readonly proposals = signal<RbacProposal[]>([]);
  readonly proposalError = signal('');
  readonly proposalLoading = signal(false);
  readonly proposalBusyId = signal<string | null>(null);
  readonly proposalRejectionReasons = signal<Readonly<Record<string, string>>>({});
  readonly proposalCancellationReasons = signal<Readonly<Record<string, string>>>({});
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');

  readonly selectedRole = computed(() =>
    this.roles().find(role => role.id === this.selectedRoleId()) ?? null
  );
  readonly moduleGroups = computed(() => {
    const groups = new Map<string, Permission[]>();
    for (const permission of this.permissions().filter(item => item.active)) {
      const items = groups.get(permission.moduleCode) ?? [];
      items.push(permission);
      groups.set(permission.moduleCode, items);
    }
    return [...groups.entries()]
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([code, permissions]) => ({ code, permissions }));
  });
  readonly effectiveSummary = computed(() =>
    this.permissions().filter(permission => this.selectedPermissionIds().has(permission.id))
  );
  newRoleCode = '';
  newRoleName = '';
  newRoleDescription = '';
  editRoleName = '';
  editRoleDescription = '';
  changeReason = '';
  deactivateCode = '';
  selectedUserId: number | null = null;
  selectedAssignmentRoleId: number | null = null;
  membershipStart = this.toLocalDateTime(new Date());
  membershipEnd = '';
  selectedMembershipId: number | null = null;
  scopeReason = '';
  private readonly proposalIdempotencyKeys = new Map<string, string>();
  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const [roles, permissions, users, departments, teams] = await Promise.all([
        firstValueFrom(this.http.get<Role[]>(`${this.api}/roles`)),
        firstValueFrom(this.http.get<Permission[]>(`${this.api}/permissions`)),
        firstValueFrom(this.http.get<User[]>(`${this.api}/users`)),
        firstValueFrom(this.http.get<Department[]>(`${this.api}/departments`)),
        firstValueFrom(this.http.get<Team[]>(`${this.api}/teams`))
      ]);
      this.roles.set(roles);
      this.permissions.set(permissions);
      this.users.set(users.filter(user => user.active));
      this.departments.set(departments.filter(department => department.active));
      this.teams.set(teams.filter(team => team.active));
      await this.loadDelegations();
      await this.loadProposals();
      const firstRole = roles.find(role => role.active);
      if (firstRole) await this.selectRole(firstRole.id);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  async loadDelegations(): Promise<void> {
    this.delegationError.set('');
    try {
      const rows = await firstValueFrom(
        this.http.get<AdminHeadDelegation[]>(`${this.api}/admin-head-delegations`)
      );
      this.delegations.set(rows);
    } catch (error) {
      this.delegations.set([]);
      this.delegationError.set(this.errorMessage(error));
    }
  }

  async loadProposals(): Promise<void> {
    this.proposalLoading.set(true);
    this.proposalError.set('');
    try {
      this.proposals.set(await firstValueFrom(
        this.http.get<RbacProposal[]>(`${this.api}/permission-proposals`)
      ));
    } catch (error) {
      this.proposals.set([]);
      this.proposalError.set(this.errorMessage(error));
    } finally {
      this.proposalLoading.set(false);
    }
  }

  setProposalRejectionReason(proposalId: string, reason: string): void {
    this.proposalRejectionReasons.update(current => ({ ...current, [proposalId]: reason }));
  }

  setProposalCancellationReason(proposalId: string, reason: string): void {
    this.proposalCancellationReasons.update(current => ({ ...current, [proposalId]: reason }));
  }

  async approveProposal(proposal: RbacProposal): Promise<void> {
    if (!proposal.canApprove || !this.proposalIsActionable(proposal) || this.proposalBusyId()) return;
    this.proposalBusyId.set(proposal.id);
    this.proposalError.set('');
    try {
      const result = await firstValueFrom(this.http.post<RbacProposal>(
        `${this.api}/permission-proposals/${proposal.id}/approve`,
        { expectedVersion: proposal.rowVersion }
      ));
      this.notice.set(`Proposal ${result.id} is ${result.status.toLowerCase()}.`);
      await this.loadProposals();
      const roleId = this.selectedRoleId();
      if (roleId) await this.selectRole(roleId);
    } catch (error) {
      const actionError = this.errorMessage(error);
      await this.loadProposals();
      const refreshError = this.proposalError();
      this.proposalError.set(refreshError ? `${actionError}; refresh failed: ${refreshError}` : actionError);
    } finally {
      this.proposalBusyId.set(null);
    }
  }

  async rejectProposal(proposal: RbacProposal): Promise<void> {
    const reason = this.proposalRejectionReasons()[proposal.id]?.trim() ?? '';
    if (!proposal.canReject || !this.proposalIsActionable(proposal) || !reason || this.proposalBusyId()) return;
    this.proposalBusyId.set(proposal.id);
    this.proposalError.set('');
    try {
      const result = await firstValueFrom(this.http.post<RbacProposal>(
        `${this.api}/permission-proposals/${proposal.id}/reject`,
        { expectedVersion: proposal.rowVersion, reason }
      ));
      this.notice.set(`Proposal ${result.id} was rejected.`);
      await this.loadProposals();
    } catch (error) {
      const actionError = this.errorMessage(error);
      await this.loadProposals();
      const refreshError = this.proposalError();
      this.proposalError.set(refreshError ? `${actionError}; refresh failed: ${refreshError}` : actionError);
    } finally {
      this.proposalBusyId.set(null);
    }
  }

  async cancelProposal(proposal: RbacProposal): Promise<void> {
    const reason = this.proposalCancellationReasons()[proposal.id]?.trim() ?? '';
    if (!proposal.canCancel || !this.proposalIsActionable(proposal) || !reason || this.proposalBusyId()) return;
    this.proposalBusyId.set(proposal.id);
    this.proposalError.set('');
    try {
      const result = await firstValueFrom(this.http.post<RbacProposal>(
        `${this.api}/permission-proposals/${proposal.id}/cancel`,
        { expectedVersion: proposal.rowVersion, reason }
      ));
      this.notice.set(`Proposal ${result.id} was cancelled.`);
      await this.loadProposals();
    } catch (error) {
      const actionError = this.errorMessage(error);
      await this.loadProposals();
      const refreshError = this.proposalError();
      this.proposalError.set(refreshError ? `${actionError}; refresh failed: ${refreshError}` : actionError);
    } finally {
      this.proposalBusyId.set(null);
    }
  }

  proposalStatus(proposal: RbacProposal): string {
    if (proposal.status === 'PENDING' && Date.parse(proposal.expiresAt) <= Date.now())
      return 'Expired · status refresh required';
    return proposal.status;
  }

  proposalIsActionable(proposal: RbacProposal): boolean {
    return proposal.status === 'PENDING' && Date.parse(proposal.expiresAt) > Date.now();
  }

  delegationUserLabel(userId: number): string {
    const user = this.users().find(item => item.id === userId);
    return user ? `${user.displayName} (${user.loginName})` : `User #${userId}`;
  }

  async selectRole(roleId: number): Promise<void> {
    this.selectedRoleId.set(roleId);
    const role = this.roles().find(item => item.id === roleId);
    this.editRoleName = role?.name ?? '';
    this.editRoleDescription = role?.description ?? '';
    this.changeReason = '';
    this.deactivateCode = '';
    this.error.set('');
    try {
      const grants = await firstValueFrom(
        this.http.get<RolePermission[]>(`${this.api}/roles/${roleId}/permissions`)
      );
      this.rolePermissions.set(grants);
      const now = Date.now();
      const granted = new Set(grants.filter(grant =>
        grant.active &&
        new Date(grant.validFrom).getTime() <= now &&
        (!grant.validTo || new Date(grant.validTo).getTime() > now)
      ).map(grant => grant.permissionId));
      this.savedPermissionIds.set(granted);
      this.selectedPermissionIds.set(new Set(granted));
      if (role) await this.loadMembershipsForRole(role.id);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    }
  }

  setPermission(permissionId: number, checked: boolean): void {
    const selected = new Set(this.selectedPermissionIds());
    if (checked) selected.add(permissionId);
    else selected.delete(permissionId);
    this.selectedPermissionIds.set(selected);
  }

  permissionChanged(permissionId: number, event: Event): void {
    if (event.target instanceof HTMLInputElement)
      this.setPermission(permissionId, event.target.checked);
  }

  isPermissionConfigurable(permission: Permission): boolean {
    return this.savedPermissionIds().has(permission.id) ||
      this.isPermissionGrantable(permission) ||
      this.isPermissionProposable(permission);
  }

  private isPermissionGrantable(permission: Permission): boolean {
    const role = this.selectedRole();
    return Boolean(role && !role.systemRole &&
      this.permissionMetadataValid(permission) &&
      !this.permissionRequiresIndependentApproval(permission) &&
      this.roleMayReceive(permission, role.code));
  }

  private isPermissionProposable(permission: Permission): boolean {
    const role = this.selectedRole();
    if (!role || role.code === 'SUPER_ADMIN' || !this.permissionMetadataValid(permission) ||
        !this.roleMayReceive(permission, role.code))
      return false;
    return role.systemRole || this.permissionRequiresIndependentApproval(permission);
  }

  private permissionMetadataValid(permission: Permission): boolean {
    const scopes = ['ORGANIZATION', 'DEPARTMENT', 'TEAM', 'ADMIN_GROUP', 'RECORD', 'NONE'];
    const securityClasses = ['READ_ONLY', 'BUSINESS', 'DEPARTMENT_ADMIN', 'ORGANIZATION_ADMIN', 'SECURITY_GOVERNANCE'];
    const readActions = ['VIEW', 'READ', 'LIST', 'EXPORT'];
    const securityClass = permission.securityClass.toUpperCase();
    return Boolean(permission.moduleCode.trim() && permission.actionCode.trim() &&
      scopes.includes(permission.scopeClass.toUpperCase()) && securityClasses.includes(securityClass) &&
      (securityClass === 'READ_ONLY') === permission.readOnly &&
      (!permission.readOnly || readActions.includes(permission.actionCode.toUpperCase())));
  }

  private roleMayReceive(permission: Permission, roleCode: string): boolean {
    const securityClass = permission.securityClass.toUpperCase();
    if (roleCode === 'SUPER_ADMIN') return false;
    if (roleCode === 'SPECTATOR') return permission.readOnly && securityClass === 'READ_ONLY';
    if (roleCode === 'USER') return securityClass === 'READ_ONLY' || securityClass === 'BUSINESS';
    if (['ADMIN', 'ADMIN_HEAD'].includes(roleCode))
      return ['READ_ONLY', 'BUSINESS', 'DEPARTMENT_ADMIN'].includes(securityClass);
    return ['READ_ONLY', 'BUSINESS', 'DEPARTMENT_ADMIN'].includes(securityClass);
  }

  private permissionRequiresIndependentApproval(permission: Permission): boolean {
    return ['DEPARTMENT_ADMIN', 'ORGANIZATION_ADMIN', 'SECURITY_GOVERNANCE']
      .includes(permission.securityClass.toUpperCase());
  }

  permissionConfigurationBlockReason(permission: Permission): string {
    if (this.isPermissionProposable(permission))
      return 'requires independent approval';
    return this.permissionRequiresIndependentApproval(permission)
      ? 'outside the supported approval policy'
      : 'invalid metadata or role ceiling';
  }

  async savePermissions(): Promise<void> {
    const role = this.selectedRole();
    if (!role || this.busy()) return;
    const changed = this.permissions().filter(permission =>
      this.selectedPermissionIds().has(permission.id) !== this.savedPermissionIds().has(permission.id)
    );
    if (!changed.length) {
      this.notice.set('No permission changes to save.');
      return;
    }
    const grants = changed.filter(permission => this.selectedPermissionIds().has(permission.id));
    if (role.code === 'SPECTATOR' && grants.some(permission =>
      !permission.readOnly || permission.securityClass.toUpperCase() !== 'READ_ONLY'
    )) {
      this.error.set('Spectator can only receive explicitly read-only capabilities.');
      return;
    }
    const proposedGrants = grants.filter(permission =>
      role.systemRole || this.permissionRequiresIndependentApproval(permission)
    );
    if (proposedGrants.length) {
      if (changed.length !== 1 || proposedGrants.length !== 1) {
        this.error.set('Submit each approval-required permission change separately.');
        return;
      }
      if (!this.isPermissionProposable(proposedGrants[0])) {
        this.error.set('This protected or sensitive permission change has no supported approval path.');
        return;
      }
      await this.createPermissionProposal(role.id, proposedGrants[0]);
      return;
    }
    if (grants.some(permission => !this.isPermissionGrantable(permission))) {
      this.error.set('Permissions with missing or invalid security metadata cannot be changed.');
      return;
    }
    if (!this.changeReason.trim()) {
      this.error.set('A reason is required for permission changes.');
      return;
    }

    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await firstValueFrom(this.http.post<void>(
        `${this.api}/roles/${role.id}/permissions/batch`,
        {
          expectedActivePermissionIds: [...this.savedPermissionIds()],
          changes: changed.map(permission => ({
            permissionId: permission.id,
            granted: this.selectedPermissionIds().has(permission.id)
          })),
          reason: this.changeReason.trim()
        }
      ));
      this.notice.set(`Saved ${changed.length} permission change(s).`);
      await this.selectRole(role.id);
    } catch (error) {
      const message = this.errorMessage(error);
      await this.selectRole(role.id);
      this.error.set(message);
    } finally {
      this.busy.set(false);
    }
  }

  private async createPermissionProposal(roleId: number, permission: Permission): Promise<void> {
    const reason = this.changeReason.trim();
    if (!reason) {
      this.error.set('A reason is required for permission proposals.');
      return;
    }
    const expectedActivePermissionIds = [...this.savedPermissionIds()].sort((left, right) => left - right);
    const requestScope = `${roleId}:${permission.id}:${expectedActivePermissionIds.join(',')}:${reason}`;

    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const storageKey = await this.proposalIdempotencyStorageKey(requestScope);
      let idempotencyKey = this.proposalIdempotencyKeys.get(requestScope) ??
        globalThis.sessionStorage.getItem(storageKey) ?? undefined;
      if (!idempotencyKey) {
        idempotencyKey = globalThis.crypto.randomUUID().replaceAll('-', '');
        globalThis.sessionStorage.setItem(storageKey, idempotencyKey);
      }
      this.proposalIdempotencyKeys.set(requestScope, idempotencyKey);
      const proposal = await firstValueFrom(this.http.post<RbacProposal>(
        `${this.api}/roles/${roleId}/permission-proposals`,
        { permissionId: permission.id, expectedActivePermissionIds, reason, idempotencyKey }
      ));
      this.proposalIdempotencyKeys.delete(requestScope);
      globalThis.sessionStorage.removeItem(storageKey);
      this.notice.set(`Submitted proposal ${proposal.id}. The permission is not active until an independent approver applies it.`);
      await this.loadProposals();
      await this.selectRole(roleId);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  private async proposalIdempotencyStorageKey(requestScope: string): Promise<string> {
    const digest = await globalThis.crypto.subtle.digest(
      'SHA-256',
      new TextEncoder().encode(requestScope)
    );
    const fingerprint = Array.from(new Uint8Array(digest), value =>
      value.toString(16).padStart(2, '0')
    ).join('');
    return `pa-tickets-rbac-proposal:${fingerprint}`;
  }

  async createRole(): Promise<void> {
    if (!this.newRoleCode.trim() || !this.newRoleName.trim() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const role = await firstValueFrom(this.http.post<Role>(`${this.api}/roles`, {
        code: this.newRoleCode.trim(),
        name: this.newRoleName.trim(),
        description: this.newRoleDescription.trim() || null
      }));
      this.newRoleCode = '';
      this.newRoleName = '';
      this.newRoleDescription = '';
      this.notice.set(`Created custom role ${role.code}. Configure its permissions and scope before assigning it.`);
      await this.load();
      await this.selectRole(role.id);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  async saveRoleDetails(): Promise<void> {
    const role = this.selectedRole();
    if (!role || role.systemRole || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.http.put<Role>(`${this.api}/roles/${role.id}`, {
        name: this.editRoleName.trim(),
        description: this.editRoleDescription.trim() || null
      }));
      this.notice.set('Custom role details saved.');
      await this.load();
      await this.selectRole(role.id);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  async deactivateRole(): Promise<void> {
    const role = this.selectedRole();
    if (!role || role.systemRole || this.deactivateCode.trim() !== role.code || !this.changeReason.trim() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.http.post<void>(`${this.api}/roles/${role.id}/deactivate`, {
        reason: this.changeReason.trim()
      }));
      this.notice.set(`Deactivated ${role.code}; its memberships and scopes were closed and audited.`);
      await this.load();
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  async assignRole(): Promise<void> {
    if (!this.selectedUserId || !this.selectedAssignmentRoleId || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const membership = await firstValueFrom(this.http.post<Membership>(`${this.api}/user-roles`, {
        userId: this.selectedUserId,
        roleId: this.selectedAssignmentRoleId,
        validFrom: new Date(this.membershipStart).toISOString(),
        validTo: this.membershipEnd ? new Date(this.membershipEnd).toISOString() : null
      }));
      this.selectedMembershipId = membership.id;
      this.notice.set('Role membership assigned. Review its effective scope before granting application access.');
      await this.loadMembershipsForRole(this.selectedAssignmentRoleId);
      await this.loadScopes(membership.id);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  async selectMembership(membershipId: number): Promise<void> {
    this.selectedMembershipId = membershipId;
    await this.loadScopes(membershipId);
  }

  async userChanged(): Promise<void> {
    this.selectedMembershipId = null;
    this.scopes.set([]);
    const roleId = this.selectedRoleId();
    if (roleId) {
      try {
        await this.loadMembershipsForRole(roleId);
      } catch (error) {
        this.error.set(this.errorMessage(error));
      }
    }
  }

  async removeScope(scopeId: number): Promise<void> {
    if (!this.scopeReason.trim() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await firstValueFrom(this.http.delete<void>(
        `${this.api}/role-scopes/${scopeId}?reason=${encodeURIComponent(this.scopeReason.trim())}`
      ));
      this.notice.set('Role scope ended and audited.');
      if (this.selectedMembershipId) await this.loadScopes(this.selectedMembershipId);
    } catch (error) {
      this.error.set(this.errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  get selectedUser(): User | undefined {
    return this.users().find(user => user.id === this.selectedUserId);
  }

  get selectedAssignmentRole(): Role | undefined {
    return this.roles().find(role => role.id === this.selectedAssignmentRoleId);
  }

  private async loadMembershipsForRole(roleId: number): Promise<void> {
    if (!this.selectedUserId) {
      this.memberships.set([]);
      return;
    }
    const memberships = await firstValueFrom(
      this.http.get<Membership[]>(`${this.api}/users/${this.selectedUserId}/roles`)
    );
    this.memberships.set(memberships.filter(item => item.roleId === roleId));
  }

  private async loadScopes(membershipId: number): Promise<void> {
    this.scopes.set(await firstValueFrom(
      this.http.get<RoleScope[]>(`${this.api}/user-roles/${membershipId}/scopes`)
    ));
  }

  private toLocalDateTime(date: Date): string {
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error;
      if (typeof body === 'string' && body.trim()) return body;
      if (typeof body === 'object' && body !== null) {
        for (const key of ['detail', 'title', 'message']) {
          const value = Reflect.get(body, key);
          if (typeof value === 'string' && value.trim()) return value;
        }
      }
      return error.message;
    }
    if (error instanceof Error && error.message.trim()) return error.message;
    return 'The request failed. Review the server response and try again.';
  }
}
