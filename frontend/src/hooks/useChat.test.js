import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor, cleanup } from '@testing-library/react';

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

import { useChat } from './useChat';

describe('useChat', () => {
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
    const { result } = renderHook(() => useChat(vi.fn(), vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });

    expect(mockStart).not.toHaveBeenCalled();
    expect(result.current.isConnected).toBe(false);
  });

  it('connects when token is present and delivers messages', async () => {
    localStorage.setItem('accessToken', 'tok');
    const onMessage = vi.fn();
    const onRead = vi.fn();
    const { result } = renderHook(() => useChat(onMessage, onRead));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });

    await waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
      expect(result.current.isConnected).toBe(true);
    });

    act(() => {
      connectionHandlers.ReceiveMessage?.({ id: 'm1', body: 'hello' });
    });
    expect(onMessage).toHaveBeenCalledWith({ id: 'm1', body: 'hello' });

    act(() => {
      connectionHandlers.MessagesRead?.('conv-1');
    });
    expect(onRead).toHaveBeenCalledWith('conv-1');
  });

  it('records connection start errors without connecting', async () => {
    const errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    localStorage.setItem('accessToken', 'tok');
    mockStart.mockRejectedValue(new Error('hub down'));

    const { result } = renderHook(() => useChat(vi.fn(), vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });

    await waitFor(() => {
      expect(result.current.isConnected).toBe(false);
    });
    expect(errSpy).toHaveBeenCalled();
    errSpy.mockRestore();
  });

  it('disconnects on unmount', async () => {
    localStorage.setItem('accessToken', 'tok');
    const { result, unmount } = renderHook(() => useChat(vi.fn(), vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });
    await waitFor(() => expect(result.current.isConnected).toBe(true));

    unmount();
    await waitFor(() => {
      expect(mockStop).toHaveBeenCalled();
    });
  });

  it('toggles connected state on reconnect lifecycle events', async () => {
    localStorage.setItem('accessToken', 'tok');
    const { result } = renderHook(() => useChat(vi.fn(), vi.fn()));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });
    await waitFor(() => expect(result.current.isConnected).toBe(true));

    act(() => {
      connectionHandlers.onreconnecting?.();
    });
    expect(result.current.isConnected).toBe(false);

    act(() => {
      connectionHandlers.onreconnected?.();
    });
    expect(result.current.isConnected).toBe(true);

    act(() => {
      connectionHandlers.onclose?.();
    });
    expect(result.current.isConnected).toBe(false);
  });
});
