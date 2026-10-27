import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface FeatureSwitchView {
  code: string;
  name: string;
  departmentId: number | null;
  enabled: boolean;
  configured: boolean;
  inherited: boolean;
  rowVersion: number;
}

export interface SetFeatureSwitchRequest {
  enabled: boolean;
  departmentId: number | null;
  expectedRowVersion: number;
  reason: string | null;
}

export interface FeatureAvailability { code: string; enabled: boolean; }

@Injectable({ providedIn: 'root' })
export class FeatureSwitchesService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/v1/admin/feature-switches`;

  availability(): Observable<readonly FeatureAvailability[]> {
    return this.http.get<readonly FeatureAvailability[]>(`${environment.apiBaseUrl}/api/v1/features/availability`);
  }

  list(departmentId: number | null): Observable<readonly FeatureSwitchView[]> {
    const params = departmentId === null ? undefined : new HttpParams().set('departmentId', departmentId);
    return this.http.get<readonly FeatureSwitchView[]>(this.endpoint, { params });
  }

  set(code: string, request: SetFeatureSwitchRequest): Observable<FeatureSwitchView> {
    return this.http.put<FeatureSwitchView>(`${this.endpoint}/${encodeURIComponent(code)}`, request);
  }
}
