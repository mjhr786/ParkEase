import { useMemo } from 'react';
import { useTheme } from '../contexts/ThemeContext';

function readVar(name, fallback) {
  if (typeof window === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

/**
 * Resolved theme color values for libraries that do not reliably honor CSS variables
 * in SVG presentation attributes (Recharts, canvas, etc.).
 */
export function useThemeColors() {
  const { theme } = useTheme();

  return useMemo(
    () => ({
      theme,
      primary: readVar('--color-primary', theme === 'light' ? '#4f46e5' : '#6366f1'),
      accentLight: readVar('--color-accent-light', theme === 'light' ? '#6366f1' : '#818cf8'),
      secondary: readVar('--color-secondary', theme === 'light' ? '#7c3aed' : '#8b5cf6'),
      success: readVar('--color-success', '#10b981'),
      warning: readVar('--color-warning', '#f59e0b'),
      error: readVar('--color-error', '#ef4444'),
      border: readVar('--color-border', theme === 'light' ? 'rgba(15,23,42,0.1)' : 'rgba(255,255,255,0.08)'),
      textPrimary: readVar('--color-text-primary', theme === 'light' ? '#0f172a' : '#ffffff'),
      textSecondary: readVar('--color-text-secondary', theme === 'light' ? '#475569' : '#a0a0b0'),
      textMuted: readVar('--color-text-muted', theme === 'light' ? '#94a3b8' : '#606070'),
      surface: readVar('--color-surface', theme === 'light' ? '#ffffff' : '#1e293b'),
      bgPrimary: readVar('--color-bg-primary', theme === 'light' ? '#f8fafc' : '#0a0a0f'),
      bgSecondary: readVar('--color-bg-secondary', theme === 'light' ? '#ffffff' : '#12121a'),
      bgTertiary: readVar('--color-bg-tertiary', theme === 'light' ? '#f1f5f9' : '#1a1a25'),
    }),
    [theme]
  );
}

export default useThemeColors;
