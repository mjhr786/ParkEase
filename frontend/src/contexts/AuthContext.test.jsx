import React, { useState } from 'react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act, cleanup } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext';

const mockLogin = vi.fn();
const mockRegister = vi.fn();
const mockLogout = vi.fn();
const mockSetTokens = vi.fn();
const mockClearTokens = vi.fn();

vi.mock('../services/api', () => ({
  default: {
    login: (...args) => mockLogin(...args),
    register: (...args) => mockRegister(...args),
    logout: (...args) => mockLogout(...args),
    setTokens: (...args) => mockSetTokens(...args),
    clearTokens: (...args) => mockClearTokens(...args),
  },
}));

function ResultProbe() {
  const auth = useAuth();
  const [last, setLast] = useState(null);
  return (
    <div>
      <button
        type="button"
        onClick={async () => {
          const r = await auth.login('a@b.com', 'pw');
          setLast(r);
        }}
      >
        login
      </button>
      <button
        type="button"
        onClick={async () => {
          const r = await auth.register({ email: 'n@b.com' });
          setLast(r);
        }}
      >
        register
      </button>
      <button
        type="button"
        onClick={async () => {
          await auth.logout();
          setLast({ loggedOut: true });
        }}
      >
        logout
      </button>
      <button
        type="button"
        onClick={() => {
          auth.updateUser({ firstName: 'Pat' });
          setLast({ updated: true });
        }}
      >
        update
      </button>
      <pre data-testid="result">{last ? JSON.stringify(last) : ''}</pre>
      <span data-testid="user">{auth.user ? JSON.stringify(auth.user) : 'none'}</span>
      <span data-testid="isAdmin">{String(auth.isAdmin)}</span>
      <span data-testid="loading">{String(auth.loading)}</span>
      <span data-testid="authenticated">{String(auth.isAuthenticated)}</span>
    </div>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it('throws when useAuth is used outside provider', () => {
    const Spy = () => {
      useAuth();
      return null;
    };
    expect(() => render(<Spy />)).toThrow('useAuth must be used within an AuthProvider');
  });

  it('hydrates user from localStorage and finishes loading', async () => {
    localStorage.setItem(
      'user',
      JSON.stringify({ email: 'stored@test.com', role: 1 })
    );

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });
    expect(screen.getByTestId('authenticated').textContent).toBe('true');
    expect(screen.getByTestId('user').textContent).toContain('stored@test.com');
    expect(screen.getByTestId('isAdmin').textContent).toBe('false');
  });

  it('treats role 0 and Admin string as isAdmin', async () => {
    localStorage.setItem(
      'user',
      JSON.stringify({ email: 'admin@test.com', role: 0 })
    );

    const { unmount } = render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );
    await waitFor(() => {
      expect(screen.getByTestId('isAdmin').textContent).toBe('true');
    });
    unmount();

    localStorage.setItem(
      'user',
      JSON.stringify({ email: 'admin2@test.com', role: 'Admin' })
    );
    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );
    await waitFor(() => {
      expect(screen.getByTestId('isAdmin').textContent).toBe('true');
    });
  });

  it('login success stores tokens and user', async () => {
    mockLogin.mockResolvedValue({
      success: true,
      data: {
        accessToken: 'at',
        refreshToken: 'rt',
        user: { email: 'a@b.com', role: 1 },
      },
    });

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'login' }).click();
    });

    await waitFor(() => {
      expect(screen.getByTestId('result').textContent).toContain('"success":true');
    });
    expect(mockSetTokens).toHaveBeenCalledWith('at', 'rt');
    expect(JSON.parse(localStorage.getItem('user')).email).toBe('a@b.com');
    expect(screen.getByTestId('authenticated').textContent).toBe('true');
  });

  it('login failure returns message without setting user', async () => {
    mockLogin.mockResolvedValue({
      success: false,
      message: 'Invalid credentials',
    });

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'login' }).click();
    });

    await waitFor(() => {
      const result = JSON.parse(screen.getByTestId('result').textContent);
      expect(result.success).toBe(false);
      expect(result.message).toBe('Invalid credentials');
    });
    expect(mockSetTokens).not.toHaveBeenCalled();
    expect(screen.getByTestId('authenticated').textContent).toBe('false');
  });

  it('login catch maps thrown API errors', async () => {
    mockLogin.mockRejectedValue({
      response: { data: { message: 'Server down' } },
    });

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'login' }).click();
    });

    await waitFor(() => {
      const result = JSON.parse(screen.getByTestId('result').textContent);
      expect(result.success).toBe(false);
      expect(result.message).toBe('Server down');
    });
  });

  it('register success stores session', async () => {
    mockRegister.mockResolvedValue({
      success: true,
      data: {
        accessToken: 'at2',
        refreshToken: 'rt2',
        user: { email: 'n@b.com', role: 1 },
      },
    });

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'register' }).click();
    });

    await waitFor(() => {
      expect(screen.getByTestId('result').textContent).toContain('"success":true');
    });
    expect(mockSetTokens).toHaveBeenCalledWith('at2', 'rt2');
    expect(screen.getByTestId('user').textContent).toContain('n@b.com');
  });

  it('register failure returns false', async () => {
    mockRegister.mockResolvedValue({
      success: false,
      message: 'Email taken',
    });

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'register' }).click();
    });

    await waitFor(() => {
      const result = JSON.parse(screen.getByTestId('result').textContent);
      expect(result.success).toBe(false);
      expect(result.message).toBe('Email taken');
    });
  });

  it('logout clears tokens even when API fails', async () => {
    localStorage.setItem(
      'user',
      JSON.stringify({ email: 'a@b.com', role: 1 })
    );
    mockLogout.mockRejectedValue(new Error('network'));

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('authenticated').textContent).toBe('true');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'logout' }).click();
    });

    await waitFor(() => {
      expect(screen.getByTestId('authenticated').textContent).toBe('false');
    });
    expect(mockClearTokens).toHaveBeenCalled();
  });

  it('updateUser merges and persists', async () => {
    localStorage.setItem(
      'user',
      JSON.stringify({ email: 'a@b.com', firstName: 'Ann', role: 1 })
    );

    render(
      <AuthProvider>
        <ResultProbe />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('user').textContent).toContain('Ann');
    });

    await act(async () => {
      screen.getByRole('button', { name: 'update' }).click();
    });

    await waitFor(() => {
      expect(screen.getByTestId('user').textContent).toContain('Pat');
    });
    expect(JSON.parse(localStorage.getItem('user')).firstName).toBe('Pat');
    expect(JSON.parse(localStorage.getItem('user')).email).toBe('a@b.com');
  });
});
