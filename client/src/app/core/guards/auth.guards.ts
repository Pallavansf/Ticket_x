import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isSessionValid()) return true;
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isSessionValid() ? inject(Router).createUrlTree(['/app']) : true;
};

export const permissionGuard: CanActivateFn = (route) => {
  const permission = route.data['permission'];
  if (typeof permission !== 'string' || inject(AuthService).hasPermission(permission)) return true;
  return inject(Router).createUrlTree(['/unauthorized']);
};


