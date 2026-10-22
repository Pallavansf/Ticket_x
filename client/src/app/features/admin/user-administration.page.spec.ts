import { HttpClient, provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { UserAdministrationPage } from './user-administration.page';

describe('UserAdministrationPage', () => {
  let fixture: ComponentFixture<UserAdministrationPage>;
  let http: { get: ReturnType<typeof vi.fn>; post: ReturnType<typeof vi.fn>; patch: ReturnType<typeof vi.fn> };
  const user = {
    id: 7, userCode: 'U7', loginName: 'user7', displayName: 'User Seven', email: null,
    departmentId: 2, teamId: 3, active: true, phone: null, managerUserId: null, emailVerified: false
  };

  beforeEach(async () => {
    http = {
      get: vi.fn((url: string) => of(url.endsWith('/users') ? [user] :
        url.endsWith('/departments') ? [{ id: 2, code: 'OPS', name: 'Operations', active: true }] :
          [{ id: 3, departmentId: 2, code: 'SUP', name: 'Support', active: true }])),
      post: vi.fn(() => of({ ...user, id: 8, userCode: 'U8', loginName: 'user8', displayName: 'User Eight' })),
      patch: vi.fn(() => of({ ...user, displayName: 'Updated User' }))
    };
    await TestBed.configureTestingModule({
      imports: [UserAdministrationPage],
      providers: [provideHttpClient(), provideRouter([]), { provide: HttpClient, useValue: http }]
    }).compileComponents();
    fixture = TestBed.createComponent(UserAdministrationPage);
    await fixture.whenStable();
  });

  it('loads users and only offers teams from the selected department', () => {
    const page = fixture.componentInstance;
    page.setCreateDepartment('2');
    expect(page.users()).toEqual([user]);
    expect(page.createTeamOptions).toEqual([{ id: 3, departmentId: 2, code: 'SUP', name: 'Support', active: true }]);
  });

  it('creates through the administration API and selects the persisted account', async () => {
    const page = fixture.componentInstance;
    page.createUserCode = 'U8'; page.createLoginName = 'user8'; page.createDisplayName = 'User Eight';
    await page.createUser();
    expect(http.post).toHaveBeenCalledWith(expect.stringMatching(/\/users$/), expect.objectContaining({
      userCode: 'U8', loginName: 'user8', displayName: 'User Eight'
    }));
    expect(page.selectedUser()?.id).toBe(8);
    expect(page.notice()).toContain('Set a temporary password');
  });

  it('saves profile edits through the API and updates the selected record', async () => {
    const page = fixture.componentInstance;
    page.selectUser(user); page.editDisplayName = 'Updated User';
    await page.saveProfile();
    expect(http.patch).toHaveBeenCalledWith(expect.stringContaining('/users/7/profile'),
      expect.objectContaining({ displayName: 'Updated User' }));
    expect(page.selectedUser()?.displayName).toBe('Updated User');
  });
});