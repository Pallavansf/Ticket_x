import { HttpClient, provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { RoleAdministrationPage } from './role-administration.page';

describe('RoleAdministrationPage', () => {
  let fixture: ComponentFixture<RoleAdministrationPage>;
  let http: {
    get: ReturnType<typeof vi.fn>;
    post: ReturnType<typeof vi.fn>;
    put: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  };

  const role = {
    id: 11, code: 'TICKET_COORDINATOR', name: 'Ticket Coordinator',
    description: null, systemRole: false, active: true
  };
  const permission = {
    id: 21, code: 'TICKET_VIEW', name: 'View tickets', description: null,
    moduleCode: 'TICKETS', actionCode: 'VIEW', scopeClass: 'TEAM',
    securityClass: 'READ_ONLY', readOnly: true, active: true
  };

  beforeEach(async () => {
    for (let index = sessionStorage.length - 1; index >= 0; index--) {
      const key = sessionStorage.key(index);
      if (key?.startsWith('pa-tickets-rbac-proposal:')) sessionStorage.removeItem(key);
    }
    http = {
      get: vi.fn((url: string) => of(
        url.endsWith('/roles') ? [role] :
          url.endsWith('/permissions') && !url.endsWith('/roles/11/permissions') ? [permission] : []
      )),
      post: vi.fn(() => of({})),
      put: vi.fn(() => of({})),
      delete: vi.fn(() => of({}))
    };
    await TestBed.configureTestingModule({
      imports: [RoleAdministrationPage],
      providers: [
        provideHttpClient(),
        { provide: HttpClient, useValue: http }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(RoleAdministrationPage);
    await fixture.whenStable();
  });

  it('does not submit a permission grant with invalid classification metadata', async () => {
    const page = fixture.componentInstance;
    page.selectedRoleId.set(role.id);
    page.permissions.set([{ ...permission, securityClass: '' }]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([permission.id]));
    page.changeReason = 'Test configuration';

    await page.savePermissions();

    expect(page.error()).toContain('missing or invalid security metadata');
    expect(http.post).not.toHaveBeenCalled();
    expect(http.put).not.toHaveBeenCalled();
  });

  it('submits a sensitive permission grant as a proposal instead of granting directly', async () => {
    const page = fixture.componentInstance;
    const highImpactPermission = {
      ...permission, securityClass: 'DEPARTMENT_ADMIN', readOnly: false
    };
    page.selectedRoleId.set(role.id);
    page.permissions.set([highImpactPermission]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([highImpactPermission.id]));
    page.changeReason = 'Reviewed grant';

    await page.savePermissions();

    expect(http.post).toHaveBeenCalledWith(
      expect.stringContaining(`/roles/${role.id}/permission-proposals`),
      expect.objectContaining({
        permissionId: highImpactPermission.id,
        expectedActivePermissionIds: [],
        reason: 'Reviewed grant',
        idempotencyKey: expect.stringMatching(/^[a-f0-9]{32}$/)
      })
    );
    expect(http.post.mock.calls[0][0]).not.toContain('/permissions/batch');
    expect(http.put).not.toHaveBeenCalled();
  });

  it('reuses the proposal idempotency key after a network failure and page recreation', async () => {
    const highImpactPermission = {
      ...permission, securityClass: 'DEPARTMENT_ADMIN', readOnly: false
    };
    const page = fixture.componentInstance;
    page.selectedRoleId.set(role.id);
    page.permissions.set([highImpactPermission]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([highImpactPermission.id]));
    page.changeReason = 'Reviewed grant';
    http.post.mockReturnValueOnce(throwError(() => new Error('network unavailable')));

    await page.savePermissions();
    const firstBody = http.post.mock.calls[0][1] as { idempotencyKey: string };
    expect(Array.from({ length: sessionStorage.length }, (_, index) =>
      sessionStorage.key(index) ?? ''
    ).every(key => !key.includes('Reviewed grant'))).toBe(true);

    fixture.destroy();
    fixture = TestBed.createComponent(RoleAdministrationPage);
    await fixture.whenStable();
    const reloadedPage = fixture.componentInstance;
    reloadedPage.selectedRoleId.set(role.id);
    reloadedPage.permissions.set([highImpactPermission]);
    reloadedPage.savedPermissionIds.set(new Set());
    reloadedPage.selectedPermissionIds.set(new Set([highImpactPermission.id]));
    reloadedPage.changeReason = 'Reviewed grant';
    http.post.mockReturnValueOnce(of({ id: 'a'.repeat(32), status: 'PENDING' }));
    await reloadedPage.savePermissions();

    const retryBody = http.post.mock.calls[1][1] as { idempotencyKey: string };
    expect(retryBody.idempotencyKey).toBe(firstBody.idempotencyKey);
    expect(Array.from({ length: sessionStorage.length }, (_, index) =>
      sessionStorage.key(index) ?? ''
    ).some(key => key.startsWith('pa-tickets-rbac-proposal:'))).toBe(false);
  });

  it('does not render an approval action for the initiator', () => {
    const page = fixture.componentInstance;
    page.proposals.set([{
      id: 'a'.repeat(32), operationType: 'ROLE_PERMISSION_GRANT', status: 'PENDING',
      targetRoleId: 11, targetRoleCode: 'TICKET_COORDINATOR', targetPermissionId: 21,
      targetPermissionCode: 'TICKET_VIEW', targetPermissionName: 'View tickets',
      moduleCode: 'TICKETS', actionCode: 'MANAGE', scopeClass: 'DEPARTMENT',
      securityClass: 'DEPARTMENT_ADMIN', initiatorUserId: 42,
      createdAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 60_000).toISOString(),
      reason: 'Reviewed grant', approverUserId: null, cancelledByUserId: null,
      decisionAt: null, decisionReason: null, applicationOutcome: null, correlationId: 'corr',
      rowVersion: 1, canApprove: false, canReject: false, canCancel: true
    }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Your proposal cannot be approved by you.');
    expect(fixture.nativeElement.textContent).not.toContain('Approve and apply');
  });

  it.each(['APPROVED', 'APPLIED', 'REJECTED', 'CANCELLED', 'EXPIRED'])(
    'renders terminal proposal state %s without decision actions',
    status => {
      const page = fixture.componentInstance;
      page.proposals.set([{
        id: 'b'.repeat(32), operationType: 'ROLE_PERMISSION_GRANT', status,
        targetRoleId: 11, targetRoleCode: 'TICKET_COORDINATOR', targetPermissionId: 21,
        targetPermissionCode: 'TICKET_VIEW', targetPermissionName: 'View tickets',
        moduleCode: 'TICKETS', actionCode: 'MANAGE', scopeClass: 'DEPARTMENT',
        securityClass: 'DEPARTMENT_ADMIN', initiatorUserId: 42,
        createdAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 60_000).toISOString(),
        reason: 'Reviewed grant', approverUserId: 43, cancelledByUserId: null,
        decisionAt: new Date().toISOString(), decisionReason: 'Decision recorded',
        applicationOutcome: status === 'APPLIED' ? 'GRANTED' : status,
        correlationId: 'corr', rowVersion: 2, canApprove: false, canReject: false, canCancel: false
      }]);
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain(status);
      expect(fixture.nativeElement.textContent).not.toContain('Approve and apply');
      expect(fixture.nativeElement.textContent).not.toContain('Cancel proposal');
    }
  );

  it('marks an expired proposal from beyond the expiry batch as expired and non-actionable', () => {
    const page = fixture.componentInstance;
    const expiredProposal = {
      id: 'c'.repeat(32), operationType: 'ROLE_PERMISSION_GRANT', status: 'PENDING',
      targetRoleId: 11, targetRoleCode: 'TICKET_COORDINATOR', targetPermissionId: 21,
      targetPermissionCode: 'TICKET_VIEW', targetPermissionName: 'View tickets',
      moduleCode: 'TICKETS', actionCode: 'MANAGE', scopeClass: 'DEPARTMENT',
      securityClass: 'DEPARTMENT_ADMIN', initiatorUserId: 42,
      createdAt: new Date(Date.now() - 90_000_000).toISOString(),
      expiresAt: new Date(Date.now() - 60_000).toISOString(),
      reason: 'Reviewed grant', approverUserId: null, cancelledByUserId: null,
      decisionAt: null, decisionReason: null, applicationOutcome: null, correlationId: 'corr',
      rowVersion: 1, canApprove: true, canReject: true, canCancel: false
    };
    page.proposals.set([expiredProposal]);
    fixture.detectChanges();

    expect(page.proposalStatus(expiredProposal)).toContain('Expired');
    expect(page.proposalIsActionable(expiredProposal)).toBe(false);
    expect(fixture.nativeElement.textContent).not.toContain('Approve and apply');
    expect(fixture.nativeElement.textContent).not.toContain('Reject');
  });

  it('reports an application failure without inventing a persisted FAILED proposal status', async () => {
    const page = fixture.componentInstance;
    const proposal = {
      id: 'd'.repeat(32), operationType: 'ROLE_PERMISSION_GRANT', status: 'PENDING',
      targetRoleId: 11, targetRoleCode: 'TICKET_COORDINATOR', targetPermissionId: 21,
      targetPermissionCode: 'TICKET_VIEW', targetPermissionName: 'View tickets',
      moduleCode: 'TICKETS', actionCode: 'MANAGE', scopeClass: 'DEPARTMENT',
      securityClass: 'DEPARTMENT_ADMIN', initiatorUserId: 42,
      createdAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 60_000).toISOString(),
      reason: 'Reviewed grant', approverUserId: null, cancelledByUserId: null,
      decisionAt: null, decisionReason: null, applicationOutcome: null, correlationId: 'corr',
      rowVersion: 1, canApprove: true, canReject: true, canCancel: false
    };
    page.proposals.set([proposal]);
    http.post.mockReturnValue(throwError(() => new Error('application failed')));
    http.get.mockReturnValueOnce(of([proposal]));

    await page.approveProposal(proposal);

    expect(page.proposalError()).toBe('application failed');
    expect(page.proposals()[0].status).toBe('PENDING');
    expect(page.proposals()[0].applicationOutcome).toBeNull();
  });

  it('allows revoking a high-impact permission to narrow access', async () => {
    http.post.mockReturnValue(of({}));
    const page = fixture.componentInstance;
    const highImpactPermission = {
      ...permission, securityClass: 'DEPARTMENT_ADMIN', readOnly: false
    };
    page.selectedRoleId.set(role.id);
    page.permissions.set([highImpactPermission]);
    page.savedPermissionIds.set(new Set([highImpactPermission.id]));
    page.selectedPermissionIds.set(new Set());
    page.changeReason = 'Remove unapproved grant';

    await page.savePermissions();

    expect(http.post).toHaveBeenCalledWith(
      expect.stringContaining(`/roles/${role.id}/permissions/batch`),
      {
        expectedActivePermissionIds: [highImpactPermission.id],
        changes: [{ permissionId: highImpactPermission.id, granted: false }],
        reason: 'Remove unapproved grant'
      }
    );
  });

  it('does not allow a Spectator grant to include a mutating permission', async () => {
    const page = fixture.componentInstance;
    page.roles.set([{ ...role, code: 'SPECTATOR', systemRole: true }]);
    page.selectedRoleId.set(role.id);
    page.permissions.set([{
      ...permission, actionCode: 'EDIT', securityClass: 'BUSINESS', readOnly: false
    }]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([permission.id]));
    page.changeReason = 'Test configuration';

    await page.savePermissions();

    expect(page.error()).toContain('Spectator can only receive explicitly read-only');
    expect(http.post).not.toHaveBeenCalled();
    expect(http.put).not.toHaveBeenCalled();
  });

  it('keeps unsaved permissions visible and reports a failed API update', async () => {
    http.post.mockReturnValue(throwError(() => new Error('forbidden')));
    const page = fixture.componentInstance;
    page.selectedRoleId.set(role.id);
    page.permissions.set([permission]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([permission.id]));
    page.changeReason = 'Reviewed grant';

    await page.savePermissions();

    expect(page.error()).toBe('forbidden');
    expect(page.selectedPermissionIds().has(permission.id)).toBe(false);
    expect(page.savedPermissionIds().has(permission.id)).toBe(false);
  });

  it('submits all permission changes with the loaded grant snapshot as one atomic request', async () => {
    http.post.mockReturnValue(of({}));

    const page = fixture.componentInstance;
    page.selectedRoleId.set(role.id);
    page.permissions.set([
      permission,
      {
        ...permission, id: 22, code: 'TICKET_EDIT', actionCode: 'EDIT',
        securityClass: 'BUSINESS', readOnly: false
      }
    ]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([permission.id, 22]));
    page.changeReason = 'Reviewed grant';

    await page.savePermissions();

    expect(http.post).toHaveBeenCalledWith(
      expect.stringContaining(`/roles/${role.id}/permissions/batch`),
      {
        expectedActivePermissionIds: [],
        changes: [
          { permissionId: permission.id, granted: true },
          { permissionId: 22, granted: true }
        ],
        reason: 'Reviewed grant'
      }
    );
    expect(page.notice()).toContain('Saved 2 permission change(s)');
    expect(http.put).not.toHaveBeenCalled();
  });

  it('reloads the saved state when an atomic permission update is rejected', async () => {
    http.post.mockReturnValue(throwError(() => new Error('snapshot conflict')));

    const page = fixture.componentInstance;
    page.selectedRoleId.set(role.id);
    page.permissions.set([permission]);
    page.savedPermissionIds.set(new Set());
    page.selectedPermissionIds.set(new Set([permission.id]));
    page.changeReason = 'Reviewed grant';

    await page.savePermissions();

    expect(page.error()).toBe('snapshot conflict');
    expect(page.selectedPermissionIds()).toEqual(new Set());
    expect(page.savedPermissionIds()).toEqual(new Set());
  });

  it('shows delegation history as read-only until independent approval is available', async () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Delegation changes are disabled until pending independent approval is implemented'
    );
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    const delegationActions = Array.from(buttons)
      .filter(button => /delegation/i.test(button.textContent ?? ''));
    expect(delegationActions).toHaveLength(0);
    expect(http.post).not.toHaveBeenCalled();
    expect(http.delete).not.toHaveBeenCalled();
  });
});
