import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

const STORAGE_KEY = 'parkease-theme';
/** User preference: explicit themes or follow OS. */
const PREFERENCES = ['dark', 'light', 'system'];
/** Resolved themes applied to the document. */
const RESOLVED = ['dark', 'light'];

const ThemeContext = createContext(null);

function getSystemTheme() {
  if (typeof window === 'undefined' || !window.matchMedia) return 'dark';
  try {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}

function resolveTheme(preference, systemTheme = getSystemTheme()) {
  if (preference === 'system') return systemTheme;
  return RESOLVED.includes(preference) ? preference : 'dark';
}

function readStoredPreference() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (PREFERENCES.includes(stored)) return stored;
  } catch {
    /* ignore private-mode / blocked storage */
  }
  return 'dark';
}

function applyThemeToDocument(resolved) {
  const root = document.documentElement;
  root.setAttribute('data-theme', resolved);
  root.style.colorScheme = resolved;
}

export function ThemeProvider({ children }) {
  const [preference, setPreferenceState] = useState(() => readStoredPreference());
  const [systemTheme, setSystemTheme] = useState(() => getSystemTheme());

  const theme = resolveTheme(preference, systemTheme);

  // Persist preference and apply resolved theme to the document
  useEffect(() => {
    applyThemeToDocument(theme);
    try {
      localStorage.setItem(STORAGE_KEY, preference);
    } catch {
      /* ignore */
    }
  }, [theme, preference]);

  // Follow OS when preference is "system"
  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return undefined;
    const mq = window.matchMedia('(prefers-color-scheme: light)');
    const onChange = (e) => setSystemTheme(e.matches ? 'light' : 'dark');
    // Sync in case FOUC / first paint differed
    setSystemTheme(mq.matches ? 'light' : 'dark');
    if (mq.addEventListener) {
      mq.addEventListener('change', onChange);
      return () => mq.removeEventListener('change', onChange);
    }
    // Safari < 14
    mq.addListener(onChange);
    return () => mq.removeListener(onChange);
  }, []);

  // Cross-tab sync
  useEffect(() => {
    const onStorage = (e) => {
      if (e.key !== STORAGE_KEY) return;
      if (PREFERENCES.includes(e.newValue)) {
        setPreferenceState(e.newValue);
      }
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const setTheme = useCallback((next) => {
    if (PREFERENCES.includes(next)) setPreferenceState(next);
  }, []);

  /** Cycle dark → light → system → dark */
  const toggleTheme = useCallback(() => {
    setPreferenceState((prev) => {
      if (prev === 'dark') return 'light';
      if (prev === 'light') return 'system';
      return 'dark';
    });
  }, []);

  const value = useMemo(
    () => ({
      /** User preference: dark | light | system */
      preference,
      /** Resolved theme applied to the document: dark | light */
      theme,
      setTheme,
      toggleTheme,
      isDark: theme === 'dark',
      isLight: theme === 'light',
      isSystem: preference === 'system',
    }),
    [preference, theme, setTheme, toggleTheme]
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return ctx;
}

export default ThemeContext;
