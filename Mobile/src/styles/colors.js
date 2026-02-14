/**
 * Color Palette
 * Premium parking app color scheme with deep blues and vibrant accents
 */

export const colors = {
    // Primary
    primary: '#2563EB',
    primaryDark: '#1D4ED8',
    primaryLight: '#3B82F6',
    primarySoft: '#DBEAFE',

    // Secondary / Accent
    accent: '#F59E0B',
    accentDark: '#D97706',
    accentLight: '#FCD34D',
    accentSoft: '#FEF3C7',

    // Success
    success: '#10B981',
    successDark: '#059669',
    successLight: '#34D399',
    successSoft: '#D1FAE5',

    // Danger
    danger: '#EF4444',
    dangerDark: '#DC2626',
    dangerLight: '#F87171',
    dangerSoft: '#FEE2E2',

    // Warning
    warning: '#F59E0B',
    warningDark: '#D97706',
    warningSoft: '#FEF3C7',

    // Info
    info: '#06B6D4',
    infoDark: '#0891B2',
    infoSoft: '#CFFAFE',

    // Neutrals
    white: '#FFFFFF',
    background: '#F8FAFC',
    surface: '#FFFFFF',
    surfaceElevated: '#FFFFFF',
    border: '#E2E8F0',
    borderLight: '#F1F5F9',
    divider: '#E2E8F0',

    // Text
    textPrimary: '#0F172A',
    textSecondary: '#475569',
    textTertiary: '#94A3B8',
    textInverse: '#FFFFFF',
    textLink: '#2563EB',

    // Dark variants (for cards, badges, etc.)
    dark: '#0F172A',
    darkGray: '#1E293B',
    mediumGray: '#64748B',
    lightGray: '#CBD5E1',

    // Gradient presets
    gradients: {
        primary: ['#2563EB', '#1D4ED8'],
        accent: ['#F59E0B', '#D97706'],
        dark: ['#0F172A', '#1E293B'],
        hero: ['#1E3A8A', '#2563EB', '#3B82F6'],
        sunset: ['#F59E0B', '#EF4444'],
        success: ['#10B981', '#059669'],
    },

    // Status colors for booking badges
    status: {
        pending: '#F59E0B',
        confirmed: '#10B981',
        inProgress: '#2563EB',
        completed: '#059669',
        cancelled: '#EF4444',
        expired: '#94A3B8',
        awaitingPayment: '#F59E0B',
        rejected: '#DC2626',
    },

    // Overlay
    overlay: 'rgba(15, 23, 42, 0.5)',
    overlayLight: 'rgba(15, 23, 42, 0.3)',
};

export default colors;
