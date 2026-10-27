import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth/auth.service';
import { RealtimeService } from './realtime.service';

const mocks = vi.hoisted(() => ({
  connection: {
    state: 'Disconnected',
    start: vi.fn(),
    stop: vi.fn(),
    invoke: vi.fn(),
    on: vi.fn(),
    off: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn()
  },
  withUrl: vi.fn(),
  withAutomaticReconnect: vi.fn(),
  configureLogging: vi.fn(),
  build: vi.fn()
}));

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected', Connecting: 'Connecting', Reconnecting: 'Reconnecting' },
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl(...args: unknown[]) { mocks.withUrl(...args); return this; }
    withAutomaticReconnect(...args: unknown[]) { mocks.withAutomaticReconnect(...args); return this; }
    configureLogging(...args: unknown[]) { mocks.configureLogging(...args); return this; }
    build() { mocks.build(); return mocks.connection; }
  }
}));

describe('RealtimeService', () => {
  const auth = { session: signal<{ accessToken: string } | null>({ accessToken: 'token-value' }) };
  let service: RealtimeService;

  beforeEach(() => {
    vi.clearAllMocks();
    mocks.connection.state = 'Disconnected';
    mocks.connection.start.mockImplementation(async () => { mocks.connection.state = 'Connected'; });
    mocks.connection.stop.mockResolvedValue(undefined);
    mocks.connection.invoke.mockResolvedValue(undefined);
    TestBed.configureTestingModule({ providers: [RealtimeService, { provide: AuthService, useValue: auth }] });
    service = TestBed.inject(RealtimeService);
  });

  afterEach(() => TestBed.resetTestingModule());

  it('connects with the current session token, automatic reconnect and logging disabled', async () => {
    await service.start();
    expect(mocks.withUrl).toHaveBeenCalledOnce();
    const [url, options] = mocks.withUrl.mock.calls[0] as [string, { accessTokenFactory: () => string }];
    expect(url).toContain('/hubs/updates');
    expect(options.accessTokenFactory()).toBe('token-value');
    expect(mocks.withAutomaticReconnect).toHaveBeenCalledOnce();
    expect(mocks.configureLogging).toHaveBeenCalledWith(6);
    expect(service.state()).toBe('connected');
  });

  it('stays disconnected without credentials and releases event handlers on stop', async () => {
    auth.session.set(null);
    await service.start();
    expect(mocks.build).not.toHaveBeenCalled();
    expect(service.state()).toBe('disconnected');

    auth.session.set({ accessToken: 'token-value' });
    await service.start();
    const handler = vi.fn();
    const unsubscribe = service.onTicketEvent(handler);
    unsubscribe();
    expect(mocks.connection.off).toHaveBeenCalledWith('ticketEvent', handler);
    await service.stop();
    expect(mocks.connection.stop).toHaveBeenCalledOnce();
    expect(service.state()).toBe('disconnected');
  });

  it('subscribes only after connection and restores ticket subscriptions after reconnect', async () => {
    await service.start();
    expect(await service.subscribeTicket(23)).toBe(true);
    expect(mocks.connection.invoke).toHaveBeenCalledWith('SubscribeTicket', 23);

    const reconnected = mocks.connection.onreconnected.mock.calls[0][0] as () => void;
    reconnected();
    expect(mocks.connection.invoke).toHaveBeenCalledTimes(2);

    await service.unsubscribeTicket(23);
    expect(mocks.connection.invoke).toHaveBeenLastCalledWith('UnsubscribeTicket', 23);
  });
});
