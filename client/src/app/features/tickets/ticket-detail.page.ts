import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink, convertToParamMap } from '@angular/router';
import { finalize, of } from 'rxjs';
import { DatePipe } from '@angular/common';
import { StatusPanelComponent } from '../../shared/status-panel.component';
import { TicketView, TicketsApiService } from '../../core/tickets-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { ChatComponent } from '../chat/chat.component';

@Component({ selector: 'app-ticket-detail', standalone: true, imports: [RouterLink, StatusPanelComponent, DatePipe, ChatComponent], templateUrl: './ticket-detail.html', styleUrl: './ticket-detail.scss' })
export class TicketDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(TicketsApiService);
  readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
  ticket: TicketView | null = null; loading = false; completing = false; pendingCompletion: string | null = null;
  actionError: 'unauthorized' | 'forbidden' | 'not-found' | 'conflict' | 'server' | 'network' | null = null;
  refreshError: 'unauthorized' | 'forbidden' | 'not-found' | 'server' | 'network' | null = null;
  error: 'unauthorized' | 'forbidden' | 'not-found' | 'server' | 'network' | null = null;
  returnQueryParams: Record<string, string> = {};
  requestCompletion(publicId: string): void { if (!this.auth.hasPermission('TICKET_UPDATE')) return; this.pendingCompletion = publicId; this.actionError = null; this.refreshError = null; }
  cancelCompletion(): void { this.pendingCompletion = null; }
  confirmCompletion(): void { if (!this.auth.hasPermission('TICKET_UPDATE') || !this.pendingCompletion || !this.ticket || this.completing) return; const publicId = this.pendingCompletion; this.completing = true; this.actionError = null; this.changeDetector.markForCheck(); this.api.completeWorkItem(publicId, crypto.randomUUID()).pipe(finalize(() => { this.completing = false; this.changeDetector.markForCheck(); })).subscribe({ next: () => { this.pendingCompletion = null; this.changeDetector.markForCheck(); this.refreshTicket(); }, error: error => this.handleActionError(error) }); }
  private refreshTicket(): void { if (!this.ticket) return; this.api.get(this.ticket.id).subscribe({ next: value => { this.ticket = value; this.refreshError = null; this.changeDetector.markForCheck(); }, error: error => this.handleRefreshError(error) }); }
  private handleRefreshError(error: unknown): void { const status = error instanceof HttpErrorResponse ? error.status : 0; this.refreshError = status === 401 ? 'unauthorized' : status === 403 ? 'forbidden' : status === 404 ? 'not-found' : status === 0 ? 'network' : 'server'; this.changeDetector.markForCheck(); }
  private handleActionError(error: unknown): void { const status = error instanceof HttpErrorResponse ? error.status : 0; this.actionError = status === 401 ? 'unauthorized' : status === 403 ? 'forbidden' : status === 404 ? 'not-found' : status === 409 ? 'conflict' : status === 0 ? 'network' : 'server'; this.changeDetector.markForCheck(); }
  statusLabel(value: number | string): string { if (typeof value === 'string') return value; return ({ 1: 'New', 2: 'Classified', 3: 'Routed', 4: 'In progress', 5: 'Waiting', 6: 'Pending approval', 7: 'Resolved', 8: 'Closed', 9: 'Cancelled', 10: 'Reopened' } as Record<number, string>)[value] ?? `Status ${value}`; }
  typeLabel(value: number): string { return ({ 1: 'Request', 2: 'Incident', 3: 'Problem', 4: 'Task', 5: 'Change' } as Record<number, string>)[value] ?? `Type ${value}`; }
  ngOnInit(): void {
    const queryMap$ = this.route.queryParamMap ?? of(this.route.snapshot.queryParamMap ?? convertToParamMap({}));
    queryMap$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(query => {
      this.returnQueryParams = Object.fromEntries(['number', 'status', 'type', 'priorityId', 'serviceId', 'categoryId', 'requestTypeId', 'clientId', 'departmentId', 'teamId', 'assigneeId', 'createdFrom', 'createdTo', 'page'].flatMap(key => {
        const value = query.get(key);
        return value === null ? [] : [[key, value]];
      }));
      this.changeDetector.markForCheck();
    });

    const paramMap$ = this.route.paramMap ?? of(this.route.snapshot.paramMap ?? convertToParamMap({}));
    paramMap$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(paramMap => {
      const id = Number(paramMap.get('id'));
      if (!Number.isSafeInteger(id) || id <= 0) {
        this.ticket = null;
        this.pendingCompletion = null;
        this.error = 'not-found';
        this.changeDetector.markForCheck();
        return;
      }
      this.loadTicket(id);
    });
  }
  private loadTicket(id: number): void {
    this.loading = true;
    this.error = null;
    this.pendingCompletion = null;
    this.changeDetector.markForCheck();
    this.api.get(id).pipe(finalize(() => { this.loading = false; this.changeDetector.markForCheck(); })).subscribe({
      next: value => { this.ticket = value; this.refreshError = null; this.actionError = null; this.error = null; this.changeDetector.markForCheck(); },
      error: value => this.handleError(value)
    });
  }
  private handleError(error: unknown): void { const status = error instanceof HttpErrorResponse ? error.status : 0; this.error = status === 401 ? 'unauthorized' : status === 403 ? 'forbidden' : status === 404 ? 'not-found' : status === 0 ? 'network' : 'server'; this.changeDetector.markForCheck(); }
}


