import { useTheme } from '../contexts/ThemeContext';

const LABELS = {
  dark: 'Dark mode (click for light)',
  light: 'Light mode (click for system)',
  system: 'System theme (click for dark)',
};

/**
 * Header control: cycles dark → light → system.
 */
export default function ThemeToggle() {
  const { preference, theme, toggleTheme, isDark, isSystem } = useTheme();

  const aria =
    preference === 'system'
      ? `System theme (${theme}). Click to switch to dark`
      : isDark
        ? 'Switch to light theme'
        : 'Switch to system theme';

  return (
    <button
      type="button"
      className="theme-toggle"
      onClick={toggleTheme}
      aria-label={aria}
      title={LABELS[preference] || LABELS.dark}
      data-theme-active={preference}
      data-theme-resolved={theme}
    >
      {isSystem ? (
        /* Monitor / system */
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <rect x="3" y="4" width="18" height="12" rx="2" stroke="currentColor" strokeWidth="2" />
          <path d="M8 20h8M12 16v4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        </svg>
      ) : isDark ? (
        /* Sun → go to light */
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <circle cx="12" cy="12" r="4" stroke="currentColor" strokeWidth="2" />
          <path
            d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
          />
        </svg>
      ) : (
        /* Moon → go to system next (after light) — show moon while in light */
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path
            d="M21 14.5A8.5 8.5 0 0 1 9.5 3 7 7 0 1 0 21 14.5z"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinejoin="round"
          />
        </svg>
      )}
    </button>
  );
}
