import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
export interface AuthSession { accessToken: string; subject?: string; displayName?: string; roles: readonly string[]; permissions: readonly string[]; expiresAt?: number; passwordRotationRequired: boolean; }
export interface LoginResponse { accessToken: string; tokenType: 'Bearer'; expiresIn: number; userId: number; loginName: string; displayName: string; passwordRotationRequired: boolean; }
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly sessionState = signal<AuthSession | null>(null);
  readonly session = this.sessionState.asReadonly();
  readonly isAuthenticated = computed(() => this.isSessionValid());
  login(loginName: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/v1/auth/login`, { loginName, password }).pipe(
      tap(response => this.setBearerToken(response.accessToken))
    );
  }
  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${environment.apiBaseUrl}/api/v1/auth/change-password`, {
      currentPassword,
      newPassword,
      confirmPassword: newPassword
    });
  }
  setBearerToken(accessToken: string): void { const claims = this.decodeClaims(accessToken); this.sessionState.set({ accessToken, subject: this.stringClaim(claims, 'sub'), displayName: this.stringClaim(claims, 'name') ?? this.stringClaim(claims, 'preferred_username'), roles: this.stringArrayClaim(claims, 'roles'), permissions: [...this.stringArrayClaim(claims, 'permissions'), ...this.stringArrayClaim(claims, 'scope')], expiresAt: typeof claims['exp'] === 'number' ? claims['exp'] : undefined, passwordRotationRequired: claims['password_change_required'] === 'true' }); }
  isSessionValid(): boolean { const session = this.sessionState(); return !!session && (!session.expiresAt || session.expiresAt * 1000 > Date.now()); }
  logout(): void { this.sessionState.set(null); }
  hasPermission(permission: string): boolean { return !!this.sessionState()?.permissions.includes(permission); }
  hasRole(role: string): boolean { return !!this.sessionState()?.roles.includes(role); }
  private decodeClaims(token: string): Record<string, unknown> { try { const payload = token.split('.')[1]; if (!payload) return {}; const normalized = payload.replace(/-/g, '+').replace(/_/g, '/'); return JSON.parse(atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '='))) as Record<string, unknown>; } catch { return {}; } }
  private stringClaim(claims: Record<string, unknown>, key: string): string | undefined { return typeof claims[key] === 'string' ? claims[key] : undefined; }
  private stringArrayClaim(claims: Record<string, unknown>, key: string): string[] { const value = claims[key]; if (Array.isArray(value)) return value.filter((item): item is string => typeof item === 'string'); if (typeof value === 'string') return value.split(' ').map(item => item.trim()).filter(Boolean); return []; }
}
