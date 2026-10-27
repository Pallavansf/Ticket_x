import { Routes } from '@angular/router';
import { authGuard, guestGuard, permissionGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/login/login').then(m => m.LoginPage) },
  { path: 'unauthorized', loadComponent: () => import('./features/unauthorized/unauthorized.page').then(m => m.UnauthorizedPage) },
  { path: 'app', canActivate: [authGuard], loadComponent: () => import('./layout/shell').then(m => m.ShellComponent), children: [
    { path: '', pathMatch: 'full', canActivate: [permissionGuard], data: { permission: 'TICKET_READ' }, loadComponent: () => import('./features/home.page').then(m => m.HomePage) },
    { path: 'tickets', canActivate: [permissionGuard], data: { permission: 'TICKET_READ' }, loadComponent: () => import('./features/tickets/ticket-list.page').then(m => m.TicketListPage) },
    { path: 'tickets/create', canActivate: [permissionGuard], data: { permission: 'TICKET_UPDATE' }, loadComponent: () => import('./features/tickets/create/ticket-create.page').then(m => m.TicketCreatePage) },
    { path: 'tickets/:id', canActivate: [permissionGuard], data: { permission: 'TICKET_READ' }, loadComponent: () => import('./features/tickets/ticket-detail.page').then(m => m.TicketDetailPage) },
    { path: 'admin/users', canActivate: [permissionGuard], data: { permission: 'USER_ADMIN' }, loadComponent: () => import('./features/admin/user-administration.page').then(m => m.UserAdministrationPage) },
    { path: 'admin/roles', canActivate: [permissionGuard], data: { permission: 'ROLE_PERMISSION_ADMIN' }, loadComponent: () => import('./features/admin/role-administration.page').then(m => m.RoleAdministrationPage) },
    { path: 'admin/feature-switches', canActivate: [permissionGuard], data: { permission: 'CONFIG_ADMIN' }, loadComponent: () => import('./features/admin/feature-switch-administration.page').then(m => m.FeatureSwitchAdministrationPage) }
  ] },
  { path: '**', redirectTo: 'app' }
];




