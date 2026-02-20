/**
 * Typography Scale
 * Consistent font sizes and weights
 */

export const typography = {
    // Font families (system fonts, Expo defaults)
    fontFamily: {
        regular: 'System',
        medium: 'System',
        bold: 'System',
    },

    // Font sizes
    size: {
        xs: 10,
        sm: 12,
        md: 14,
        base: 16,
        lg: 18,
        xl: 20,
        '2xl': 24,
        '3xl': 30,
        '4xl': 36,
        '5xl': 48,
    },

    // Font weights
    weight: {
        regular: '400',
        medium: '500',
        semibold: '600',
        bold: '700',
        extrabold: '800',
    },

    // Line heights
    lineHeight: {
        tight: 1.2,
        normal: 1.5,
        relaxed: 1.75,
    },

    // Presets
    h1: {
        fontSize: 30,
        fontWeight: '700',
        lineHeight: 36,
    },
    h2: {
        fontSize: 24,
        fontWeight: '700',
        lineHeight: 30,
    },
    h3: {
        fontSize: 20,
        fontWeight: '600',
        lineHeight: 26,
    },
    h4: {
        fontSize: 18,
        fontWeight: '600',
        lineHeight: 24,
    },
    body: {
        fontSize: 16,
        fontWeight: '400',
        lineHeight: 24,
    },
    bodySmall: {
        fontSize: 14,
        fontWeight: '400',
        lineHeight: 20,
    },
    caption: {
        fontSize: 12,
        fontWeight: '400',
        lineHeight: 16,
    },
    label: {
        fontSize: 14,
        fontWeight: '500',
        lineHeight: 20,
    },
    button: {
        fontSize: 16,
        fontWeight: '600',
        lineHeight: 20,
    },
};

export default typography;
