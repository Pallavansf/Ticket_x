import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';

export interface MessageCenterItem {
  id: string;
  title: string;
  summary: string;
  category: string;
  createdAt: string;
  read: boolean;
  archived: boolean;
  deepLink?: string | null;
}

export interface MessageCenterPreferences {
  categories: readonly { code: string; label: string; enabled: boolean; mandatory: boolean }[];
}

export interface MessageCenterClient {
  list(filter: { search: string; state: 'all' | 'unread' | 'read' }): Observable<readonly MessageCenterItem[]>;
  markRead(id: string): Observable<void>;
  archive(id: string): Observable<void>;
  preferences(): Observable<MessageCenterPreferences>;
  savePreferences(value: MessageCenterPreferences): Observable<void>;
}

// S4-08 has not established an inbox API contract yet. Keep the shell usable and
// expose an explicit unavailable state until an adapter is provided.
export const MESSAGE_CENTER_CLIENT = new InjectionToken<MessageCenterClient | null>(
  'MESSAGE_CENTER_CLIENT',
  { providedIn: 'root', factory: () => null }
);
