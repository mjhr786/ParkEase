import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor, cleanup } from '@testing-library/react';
import { AUTH_CHANGED_EVENT } from '../utils/authEvents';

const mockStart = vi.fn();
const mockStop = vi.fn();
const connectionHandlers = {};
let connectionInstance;

function resetConnectionMocks() {
  Object.keys(connectionHandlers).forEach((k) => delete connectionHandlers[k]);
  mockStart.mockReset();
  mockStop.mockReset();
  mockStart.mockResolvedValue(undefined);
  mockStop.mockResolvedValue(undefined);
  connectionInstance = {
    on: vi.fn((event, cb) => {
      connectionHandlers[event] = cb;
    }),
    onclose: vi.fn((cb) => {
      connectionHandlers.onclose = cb;
    }),
    onreconnecting: vi.fn((cb) => {
      connectionHandlers.onreconnecting = cb;
    }),
    onreconnected: vi.fn((cb) => {
      connectionHandlers.onreconnected = cb;
    }),
    start: mockStart,
    stop: mockStop,
    serverTimeoutInMilliseconds: 0,
    keepAliveIntervalInMilliseconds: 0,
  };
}

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn().mockImplementation(() => ({
    withUrl: vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging: vi.fn().mockReturnThis(),
    build: vi.fn(() => connectionInstance),
  })),
  HttpTransportType: { WebSockets: 1 },
  LogLevel: { Information: 1, Warning: 2 },
}));

vi.mock('../config', () => ({
  API_BASE_URL: 'http://localhost:5000',
}));

import { useNotifications } from './useNotifications';

describe('useNotifications', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    resetConnectionMocks();
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    localStorage.clear();
  });

  it('does not connect without access token', async () => {
    const { result } = renderHook(() => useNotifications(vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600);
    });

    expect(mockStart).not.toHaveBeenCalled();
    expect(result.current.isConnected).toBe(false);
  });

  it('connects when token is present and delivers notifications', async () => {
    localStorage.setItem('accessToken', 'tok');
    const onNotification = vi.fn();
    const { result } = renderHook(() => useNotifications(onNotification));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600);
    });

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
      expect(result.current.isConnected).toBe(true);
    });

    act(() => {
      connectionHandlers.ReceiveNotification?.({ id: 'n1', title: 'Hi' });
    });
    expect(onNotification).toHaveBeenCalledWith({ id: 'n1', title: 'Hi' });
  });

  it('disconnects on auth-changed when token cleared', async () => {
    localStorage.setItem('accessToken', 'tok');
    const { result } = renderHook(() => useNotifications(vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600);
    });
    await waitFor(() => expect(result.current.isConnected).toBe(true));

    localStorage.removeItem('accessToken');
    await act(async () => {
      window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
    });

    await waitFor(() => {
      expect(mockStop).toHaveBeenCalled();
      expect(result.current.isConnected).toBe(false);
    });
  });

  it('records connection start errors', async () => {
    const errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    localStorage.setItem('accessToken', 'tok');
    mockStart.mockRejectedValue(new Error('hub down'));

    const { result } = renderHook(() => useNotifications(vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600);
    });

    await waitFor(() => {
      expect(result.current.connectionError).toBe('hub down');
      expect(result.current.isConnected).toBe(false);
    });
    errSpy.mockRestore();
  });
});
