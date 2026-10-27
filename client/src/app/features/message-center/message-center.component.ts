import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MESSAGE_CENTER_CLIENT, MessageCenterItem, MessageCenterPreferences } from './message-center.client';

@Component({
  selector: 'app-message-center',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './message-center.component.html',
  styleUrl: './message-center.component.scss'
})
export class MessageCenterComponent {
  private readonly client = inject(MESSAGE_CENTER_CLIENT);
  private readonly changeDetector = inject(ChangeDetectorRef);
  open = false;
  preferencesOpen = false;
  search = '';
  state: 'all' | 'unread' | 'read' = 'all';
  items: readonly MessageCenterItem[] = [];
  unreadCount = 0;
  preferences: MessageCenterPreferences | null = null;
  loading = false;
  saving = false;
  error = false;
  preferencesError = false;
  liveAvailable = false;

  toggle(): void {
    this.open = !this.open;
    if (this.open) this.load();
  }

  load(): void {
    if (!this.client) { this.error = true; return; }
    this.loading = true;
    this.error = false;
    this.client.list({ search: this.search, state: this.state }).subscribe({
      next: value => {
        this.items = value;
        if (this.state === 'all' && !this.search) this.unreadCount = value.filter(item => !item.read && !item.archived).length;
        this.loading = false;
        this.changeDetector.markForCheck();
      },
      error: () => { this.loading = false; this.error = true; this.changeDetector.markForCheck(); }
    });
  }

  setState(state: 'all' | 'unread' | 'read'): void { this.state = state; this.load(); }
  searchChanged(): void { this.load(); }

  markRead(item: MessageCenterItem): void {
    if (!this.client || item.read) return;
    this.client.markRead(item.id).subscribe({ next: () => { this.items = this.items.map(value => value.id === item.id ? { ...value, read: true } : value); this.unreadCount = Math.max(0, this.unreadCount - 1); this.changeDetector.markForCheck(); }, error: () => { this.error = true; this.changeDetector.markForCheck(); } });
  }

  archive(item: MessageCenterItem): void {
    if (!this.client) return;
    this.client.archive(item.id).subscribe({ next: () => { this.items = this.items.filter(value => value.id !== item.id); if (!item.read) this.unreadCount = Math.max(0, this.unreadCount - 1); this.changeDetector.markForCheck(); }, error: () => { this.error = true; this.changeDetector.markForCheck(); } });
  }

  showPreferences(): void {
    this.preferencesOpen = true;
    this.preferencesError = false;
    if (!this.client) { this.preferencesError = true; return; }
    this.client.preferences().subscribe({ next: value => { this.preferences = value; this.changeDetector.markForCheck(); }, error: () => { this.preferencesError = true; this.changeDetector.markForCheck(); } });
  }

  savePreferences(): void {
    if (!this.client || !this.preferences) return;
    this.saving = true;
    this.preferencesError = false;
    this.client.savePreferences(this.preferences).subscribe({ next: () => { this.saving = false; this.changeDetector.markForCheck(); }, error: () => { this.saving = false; this.preferencesError = true; this.changeDetector.markForCheck(); } });
  }

  setPreference(index: number, enabled: boolean): void {
    if (!this.preferences) return;
    this.preferences = { ...this.preferences, categories: this.preferences.categories.map((item, i) => i === index && !item.mandatory ? { ...item, enabled } : item) };
  }
}
