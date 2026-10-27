import { Injectable, OnDestroy, effect, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AuthService } from './auth/auth.service';
import { environment } from '../../environments/environment';

export interface RealtimeTicketEvent {
  id: number;
  type: string;
  changeKind: string;
  version: number;
  occurredAt: string;
}

export type RealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

@Injectable({ providedIn: 'root' })
export class RealtimeService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private connection?: HubConnection;
  private connectionStateHandlersBound = false;
  private readonly ticketEventHandlers = new Set<(event: RealtimeTicketEvent) => void>();
  private readonly ticketSubscriptions = new Set<number>();
  readonly state = signal<RealtimeConnectionState>('disconnected');

  constructor() {
    effect(() => {
      if (!this.auth.session()) void this.stop();
    });
  }

  async start(): Promise<void> {
    const session = this.auth.session();
    if (!session?.accessToken) {
      this.state.set('disconnected');
      return;
    }

    this.connection ??= new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/updates`, {
        accessTokenFactory: () => this.auth.session()?.accessToken ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
    this.bindConnectionState(this.connection);
    if (this.connection.state === HubConnectionState.Connected ||
        this.connection.state === HubConnectionState.Connecting ||
        this.connection.state === HubConnectionState.Reconnecting) return;

    this.state.set('connecting');
    try {
      await this.connection.start();
      if (!this.auth.session()?.accessToken) {
        await this.stop();
        return;
      }
      this.state.set('connected');
    } catch {
      this.state.set('disconnected');
    }
  }

  onTicketEvent(handler: (event: RealtimeTicketEvent) => void): () => void {
    if (!this.ticketEventHandlers.has(handler)) {
      this.ticketEventHandlers.add(handler);
      this.connection?.on('ticketEvent', handler);
    }
    return () => {
      this.ticketEventHandlers.delete(handler);
      this.connection?.off('ticketEvent', handler);
    };
  }

  async subscribeTicket(ticketId: number): Promise<boolean> {
    if (!Number.isSafeInteger(ticketId) || ticketId <= 0 || !this.connection ||
        this.connection.state !== HubConnectionState.Connected) return false;
    try {
      await this.connection.invoke('SubscribeTicket', ticketId);
      this.ticketSubscriptions.add(ticketId);
      return true;
    } catch {
      return false;
    }
  }

  async unsubscribeTicket(ticketId: number): Promise<void> {
    if (!this.ticketSubscriptions.has(ticketId)) return;
    this.ticketSubscriptions.delete(ticketId);
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try { await this.connection.invoke('UnsubscribeTicket', ticketId); } catch { }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = undefined;
    this.connectionStateHandlersBound = false;
    this.ticketSubscriptions.clear();
    this.state.set('disconnected');
    if (!connection) return;
    for (const handler of this.ticketEventHandlers) connection.off('ticketEvent', handler);
    await connection.stop();
  }

  ngOnDestroy(): void {
    void this.stop().catch(() => this.state.set('disconnected'));
  }

  private bindConnectionState(connection: HubConnection): void {
    if (this.connectionStateHandlersBound) return;
    this.connectionStateHandlersBound = true;
    connection.onreconnecting(() => this.state.set('reconnecting'));
    connection.onreconnected(() => {
      this.state.set('connected');
      for (const ticketId of this.ticketSubscriptions) {
        void connection.invoke('SubscribeTicket', ticketId).catch(() => undefined);
      }
    });
    connection.onclose(() => this.state.set('disconnected'));
    for (const handler of this.ticketEventHandlers) connection.on('ticketEvent', handler);
  }
}
