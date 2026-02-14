/**
 * Global Styles
 * Reusable style objects used across the app
 */

import { StyleSheet } from 'react-native';
import colors from './colors';
import spacing from './spacing';
import typography from './typography';
import shadows from './shadows';

export const globalStyles = StyleSheet.create({
    // Containers
    screen: {
        flex: 1,
        backgroundColor: colors.background,
    },
    screenPadded: {
        flex: 1,
        backgroundColor: colors.background,
        paddingHorizontal: spacing.screenHorizontal,
    },
    center: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    row: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    rowBetween: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
    },

    // Cards
    card: {
        backgroundColor: colors.surface,
        borderRadius: spacing.cardRadius,
        padding: spacing.cardPadding,
        marginBottom: spacing.cardMargin,
        ...shadows.card,
    },

    // Sections
    section: {
        marginBottom: spacing.xl,
    },
    sectionTitle: {
        ...typography.h3,
        color: colors.textPrimary,
        marginBottom: spacing.md,
    },

    // Divider
    divider: {
        height: 1,
        backgroundColor: colors.divider,
        marginVertical: spacing.base,
    },
});

export { colors, spacing, typography, shadows };
export default globalStyles;
