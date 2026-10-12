import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/login/login').then(m => m.LoginPage) },
  { path: 'unauthorized', loadComponent: () => import('./features/unauthorized/unauthorized.page').then(m => m.UnauthorizedPage) },
  { path: 'app', canActivate: [authGuard], loadComponent: () => import('./layout/shell').then(m => m.ShellComponent), children: [
    { path: '', pathMatch: 'full', loadComponent: () => import('./features/home.page').then(m => m.HomePage) },
    { path: 'tickets', loadComponent: () => import('./features/tickets/ticket-list.page').then(m => m.TicketListPage) },
    { path: 'tickets/create', loadComponent: () => import('./features/tickets/create/ticket-create.page').then(m => m.TicketCreatePage) },
    { path: 'tickets/:id', loadComponent: () => import('./features/tickets/ticket-detail.page').then(m => m.TicketDetailPage) }
  ] },
  { path: '**', redirectTo: 'app' }
];






