import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface TicketHistoryView { id: number; action: string; actorId: number; occurredAt: string; oldValue?: string | null; newValue?: string | null; reason?: string | null; category: string; }
export interface TicketView { id: number; number: string; type: number; status: number; subject: string; priorityId: number | null; currentDepartmentId: number | null; currentTeamId: number | null; currentAssignee: number | null; workItems: readonly WorkItemView[]; serviceId?: number | null; categoryId?: number | null; requestTypeId?: number | null; requestTypeVersion?: number | null; history?: readonly TicketHistoryView[]; }
export interface WorkItemView { id: number; number: string; publicId: string; status: string; assignedToUserId?: number | null; waitingFor?: string | null; waitingMinutes: number; }
export interface TicketSearchPage { items: readonly TicketView[]; page: number; pageSize: number; totalCount: number; }
export interface TicketSearchFilters { number?: string; status?: number; type?: number; priorityId?: number; serviceId?: number; categoryId?: number; requestTypeId?: number; clientId?: number; departmentId?: number; teamId?: number; assigneeId?: number; createdFrom?: string; createdTo?: string; page: number; pageSize: number; }
export interface CreateTicketRequest { type: number; subject: string; description?: string; requestTypeId?: number; serviceId?: number; categoryId?: number; subCategoryId?: number; fieldValues?: readonly TicketFieldValue[]; }
export interface TicketFieldValue { fieldId: number; stringValue?: string; numberValue?: number; dateValue?: string; textValue?: string; }
export interface CatalogueService { id: number; code: string; name: string; active: boolean; effectiveFrom: string; effectiveTo: string | null; }
export interface CatalogueCategory { id: number; serviceId: number; code: string; name: string; active: boolean; effectiveFrom: string; effectiveTo: string | null; }
export interface CatalogueSubCategory { id: number; categoryId: number; code: string; name: string; active: boolean; effectiveFrom: string; effectiveTo: string | null; }
export interface CatalogueRequestType { id: number; serviceId: number; categoryId: number; subCategoryId?: number | null; ticketType: number; code: string; name: string; active: boolean; defaultPriorityId?: number | null; evidenceRequired: boolean; evidenceMinimumCount: number; versionNo: number; status: string; effectiveFrom: string; effectiveTo: string | null; }

@Injectable({ providedIn: 'root' })
export class TicketsApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}/api/v1/tickets`;

  completeWorkItem(publicId: string, idempotencyKey: string): Observable<unknown> { return this.http.post<unknown>(environment.apiBaseUrl + '/api/v1/work-items/' + encodeURIComponent(publicId) + '/complete', {}, { headers: { 'Idempotency-Key': idempotencyKey } }); }
  create(request: CreateTicketRequest, idempotencyKey: string): Observable<TicketView> { return this.http.post<TicketView>(this.endpoint, request, { headers: { 'Idempotency-Key': idempotencyKey } }); }
  services(): Observable<readonly CatalogueService[]> { return this.http.get<readonly CatalogueService[]>(environment.apiBaseUrl + '/api/v1/admin/catalogue/services'); }
  categories(serviceId?: number): Observable<readonly CatalogueCategory[]> { return this.http.get<readonly CatalogueCategory[]>(environment.apiBaseUrl + '/api/v1/admin/catalogue/categories', { params: serviceId === undefined ? undefined : { serviceId } }); }
  subCategories(serviceId?: number, categoryId?: number): Observable<readonly CatalogueSubCategory[]> { return this.http.get<readonly CatalogueSubCategory[]>(environment.apiBaseUrl + '/api/v1/admin/catalogue/sub-categories', { params: { ...(serviceId === undefined ? {} : { serviceId }), ...(categoryId === undefined ? {} : { categoryId }) } }); }
  requestTypes(serviceId?: number, categoryId?: number, subCategoryId?: number): Observable<readonly CatalogueRequestType[]> { return this.http.get<readonly CatalogueRequestType[]>(environment.apiBaseUrl + '/api/v1/admin/catalogue/request-types', { params: { ...(serviceId === undefined ? {} : { serviceId }), ...(categoryId === undefined ? {} : { categoryId }), ...(subCategoryId === undefined ? {} : { subCategoryId }) } }); }
  get(id: number): Observable<TicketView> { return this.http.get<TicketView>(this.endpoint + '/' + id); }
  search(filters: TicketSearchFilters): Observable<TicketSearchPage> { let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize); for (const [key, value] of Object.entries(filters)) if (key !== 'page' && key !== 'pageSize' && value !== undefined && value !== '') params = params.set(key, value); return this.http.get<TicketSearchPage>(this.endpoint, { params }); }
}
