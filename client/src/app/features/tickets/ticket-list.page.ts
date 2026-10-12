import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { finalize, of } from 'rxjs';
import { TicketsApiService, TicketSearchFilters, TicketSearchPage } from '../../core/tickets-api.service';
import { StatusPanelComponent } from '../../shared/status-panel.component';

@Component({ selector: 'app-ticket-list', standalone: true, imports: [ReactiveFormsModule, RouterLink, StatusPanelComponent], templateUrl: './ticket-list.html', styleUrl: './ticket-list.scss' })
export class TicketListPage implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(TicketsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private suppressRouteRefresh = false;
  readonly filters = this.formBuilder.nonNullable.group({ number: [''], status: [''], type: [''], createdFrom: [''], createdTo: [''] });
  result: TicketSearchPage | null = null;
  loading = false;
  error: 'unauthorized' | 'forbidden' | 'server' | 'network' | null = null;
  page = 1;
  readonly pageSize = 25;

  ngOnInit(): void {
    const queryMap$ = this.route.queryParamMap ?? of(this.route.snapshot.queryParamMap ?? convertToParamMap({}));
    queryMap$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(query => {
      if (this.suppressRouteRefresh) {
        this.suppressRouteRefresh = false;
        return;
      }
      this.filters.patchValue({ number: query.get('number') ?? '', status: query.get('status') ?? '', type: query.get('type') ?? '', createdFrom: query.get('createdFrom') ?? '', createdTo: query.get('createdTo') ?? '' });
      const parsedPage = Number(query.get('page'));
      this.page = Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
      this.load();
    });
  }

  get listQueryParams(): Record<string, string | number | null> {
    const raw = this.filters.getRawValue();
    return { number: raw.number || null, status: raw.status || null, type: raw.type || null, createdFrom: raw.createdFrom || null, createdTo: raw.createdTo || null, page: this.page > 1 ? this.page : null };
  }

  load(): void { this.loading = true; this.error = null; this.api.search(this.toCriteria()).pipe(finalize(() => this.loading = false)).subscribe({ next: value => this.result = value, error: value => this.handleError(value) }); }
  submit(): void { this.page = 1; this.updateUrlAndLoad(); }
  previous(): void { if (this.page > 1) { this.page--; this.updateUrlAndLoad(); } }
  get totalPages(): number { return this.result ? Math.max(1, Math.ceil(this.result.totalCount / this.result.pageSize)) : 1; }
  next(): void { if (this.result && this.page * this.result.pageSize < this.result.totalCount) { this.page++; this.updateUrlAndLoad(); } }
  statusLabel(value: number): string { return ({ 1: 'New', 2: 'Classified', 3: 'Routed', 4: 'In progress', 5: 'Waiting', 6: 'Pending approval', 7: 'Resolved', 8: 'Closed', 9: 'Cancelled', 10: 'Reopened' } as Record<number, string>)[value] ?? `Status ${value}`; }
  typeLabel(value: number): string { return ({ 1: 'Request', 2: 'Incident', 3: 'Problem', 4: 'Task', 5: 'Change' } as Record<number, string>)[value] ?? `Type ${value}`; }
  private updateUrlAndLoad(): void {
    this.suppressRouteRefresh = true;
    void this.router.navigate([], { relativeTo: this.route, queryParams: this.listQueryParams, replaceUrl: true });
    this.load();
  }
  private toCriteria(): TicketSearchFilters { const raw = this.filters.getRawValue(); return { number: raw.number || undefined, status: this.numberOrUndefined(raw.status), type: this.numberOrUndefined(raw.type), createdFrom: raw.createdFrom ? new Date(`${raw.createdFrom}T00:00:00Z`).toISOString() : undefined, createdTo: raw.createdTo ? new Date(`${raw.createdTo}T23:59:59.999Z`).toISOString() : undefined, page: this.page, pageSize: this.pageSize }; }
  private numberOrUndefined(value: string): number | undefined { return value === '' ? undefined : Number(value); }
  private handleError(error: unknown): void { const status = error instanceof HttpErrorResponse ? error.status : 0; this.error = status === 401 ? 'unauthorized' : status === 403 ? 'forbidden' : status >= 500 ? 'server' : status === 0 ? 'network' : 'server'; }
}
