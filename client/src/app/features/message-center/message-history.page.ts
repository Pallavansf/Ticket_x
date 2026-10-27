import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FeatureSwitchesService } from '../../core/feature-switches.service';
import { MESSAGE_CENTER_CLIENT, MessageCenterItem } from './message-center.client';

@Component({
  selector: 'app-message-history',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './message-history.page.html',
  styleUrl: './message-history.page.scss'
})
export class MessageHistoryPage implements OnInit {
  readonly client = inject(MESSAGE_CENTER_CLIENT);
  private readonly featureSwitches = inject(FeatureSwitchesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);

  availabilityLoading = true;
  availabilityError = false;
  enabled = false;
  loading = false;
  error = false;
  actionError = false;
  search = '';
  state: 'all' | 'unread' | 'read' = 'all';
  items: readonly MessageCenterItem[] = [];

  ngOnInit(): void { this.checkAvailability(); }

  checkAvailability(): void {
    this.availabilityLoading = true;
    this.availabilityError = false;
    this.featureSwitches.availability().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: values => {
        this.enabled = values.some(value => value.code === 'MESSAGE_CENTER' && value.enabled);
        this.availabilityLoading = false;
        this.changeDetector.markForCheck();
        if (this.enabled) this.load();
      },
      error: () => {
        this.enabled = false;
        this.availabilityLoading = false;
        this.availabilityError = true;
        this.changeDetector.markForCheck();
      }
    });
  }

  load(): void {
    if (!this.enabled || !this.client) return;
    this.loading = true;
    this.error = false;
    this.client.list({ search: this.search, state: this.state }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => {
        this.items = items;
        this.loading = false;
        this.changeDetector.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.error = true;
        this.changeDetector.markForCheck();
      }
    });
  }

  searchChanged(): void { this.load(); }
  setState(value: 'all' | 'unread' | 'read'): void { this.state = value; this.load(); }

  markRead(item: MessageCenterItem): void {
    if (!this.client || item.read) return;
    this.actionError = false;
    this.client.markRead(item.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.items = this.items.map(value => value.id === item.id ? { ...value, read: true } : value);
        this.changeDetector.markForCheck();
      },
      error: () => { this.actionError = true; this.changeDetector.markForCheck(); }
    });
  }

  archive(item: MessageCenterItem): void {
    if (!this.client) return;
    this.actionError = false;
    this.client.archive(item.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.items = this.items.filter(value => value.id !== item.id);
        this.changeDetector.markForCheck();
      },
      error: () => { this.actionError = true; this.changeDetector.markForCheck(); }
    });
  }
}
