import { Injectable, computed, signal } from '@angular/core';
import { Observable, throwError } from 'rxjs';
export interface AuthSession { accessToken: string; subject?: string; displayName?: string; roles: readonly string[]; permissions: readonly string[]; expiresAt?: number; }
export class AuthIntegrationGapError extends Error { constructor() { super('The backend does not expose a login endpoint. Configure the approved OIDC client or provide a bearer token through the host integration.'); this.name = 'AuthIntegrationGapError'; } }
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly sessionState = signal<AuthSession | null>(null);
  readonly session = this.sessionState.asReadonly();
  readonly isAuthenticated = computed(() => this.isSessionValid());
  login(_identifier: string, _password: string): Observable<never> { return throwError(() => new AuthIntegrationGapError()); }
  setBearerToken(accessToken: string): void { const claims = this.decodeClaims(accessToken); this.sessionState.set({ accessToken, subject: this.stringClaim(claims, 'sub'), displayName: this.stringClaim(claims, 'name') ?? this.stringClaim(claims, 'preferred_username'), roles: this.stringArrayClaim(claims, 'roles'), permissions: [...this.stringArrayClaim(claims, 'permissions'), ...this.stringArrayClaim(claims, 'scope')], expiresAt: typeof claims['exp'] === 'number' ? claims['exp'] : undefined }); }
  isSessionValid(): boolean { const session = this.sessionState(); return !!session && (!session.expiresAt || session.expiresAt * 1000 > Date.now()); }
  logout(): void { this.sessionState.set(null); }
  hasPermission(permission: string): boolean { const session = this.sessionState(); return !!session && (session.permissions.includes(permission) || session.roles.includes(permission)); }
  private decodeClaims(token: string): Record<string, unknown> { try { const payload = token.split('.')[1]; if (!payload) return {}; const normalized = payload.replace(/-/g, '+').replace(/_/g, '/'); return JSON.parse(atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '='))) as Record<string, unknown>; } catch { return {}; } }
  private stringClaim(claims: Record<string, unknown>, key: string): string | undefined { return typeof claims[key] === 'string' ? claims[key] : undefined; }
  private stringArrayClaim(claims: Record<string, unknown>, key: string): string[] { const value = claims[key]; if (Array.isArray(value)) return value.filter((item): item is string => typeof item === 'string'); if (typeof value === 'string') return value.split(' ').map(item => item.trim()).filter(Boolean); return []; }
}
