import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, act, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider, useTheme } from './ThemeContext';

function Probe() {
  const { theme, preference, toggleTheme, isDark, isLight, isSystem } = useTheme();
  return (
    <div>
      <span data-testid="theme">{theme}</span>
      <span data-testid="preference">{preference}</span>
      <span data-testid="is-dark">{String(isDark)}</span>
      <span data-testid="is-light">{String(isLight)}</span>
      <span data-testid="is-system">{String(isSystem)}</span>
      <button type="button" onClick={toggleTheme}>
        toggle
      </button>
    </div>
  );
}

describe('ThemeProvider', () => {
  beforeEach(() => {
    cleanup();
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.style.colorScheme = '';
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.style.colorScheme = '';
  });

  it('defaults to dark when nothing is stored', () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>
    );
    expect(screen.getByTestId('theme').textContent).toBe('dark');
    expect(screen.getByTestId('preference').textContent).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('parkease-theme')).toBe('dark');
  });

  it('restores theme from localStorage', () => {
    localStorage.setItem('parkease-theme', 'light');
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>
    );
    expect(screen.getByTestId('theme').textContent).toBe('light');
    expect(screen.getByTestId('preference').textContent).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('resolves system preference to OS scheme', () => {
    localStorage.setItem('parkease-theme', 'system');
    const mql = {
      matches: true,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
    };
    vi.spyOn(window, 'matchMedia').mockImplementation(() => mql);

    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>
    );

    expect(screen.getByTestId('preference').textContent).toBe('system');
    expect(screen.getByTestId('is-system').textContent).toBe('true');
    expect(screen.getByTestId('theme').textContent).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('cycles dark → light → system and persists preference', async () => {
    const user = userEvent.setup();
    const mql = {
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
    };
    vi.spyOn(window, 'matchMedia').mockImplementation(() => mql);

    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>
    );

    expect(screen.getByTestId('is-dark').textContent).toBe('true');

    await user.click(screen.getByRole('button', { name: 'toggle' }));
    expect(screen.getByTestId('preference').textContent).toBe('light');
    expect(screen.getByTestId('is-light').textContent).toBe('true');
    expect(localStorage.getItem('parkease-theme')).toBe('light');

    await user.click(screen.getByRole('button', { name: 'toggle' }));
    expect(screen.getByTestId('preference').textContent).toBe('system');
    expect(screen.getByTestId('is-system').textContent).toBe('true');
    expect(screen.getByTestId('theme').textContent).toBe('dark');
    expect(localStorage.getItem('parkease-theme')).toBe('system');

    await user.click(screen.getByRole('button', { name: 'toggle' }));
    expect(screen.getByTestId('preference').textContent).toBe('dark');
  });

  it('syncs preference from other tabs via storage event', () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>
    );

    act(() => {
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: 'parkease-theme',
          newValue: 'light',
        })
      );
    });

    expect(screen.getByTestId('theme').textContent).toBe('light');
    expect(screen.getByTestId('preference').textContent).toBe('light');
  });

  it('throws when useTheme is used outside provider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => render(<Probe />)).toThrow(/useTheme must be used within a ThemeProvider/);
    spy.mockRestore();
  });
});
