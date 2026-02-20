/**
 * Badge Component
 * Status badge for booking/payment statuses
 */

import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { BookingStatusLabels, PaymentStatusLabels } from '../../utils/constants';

const statusColors = {
    0: { bg: colors.accentSoft, text: colors.accentDark },      // Pending
    1: { bg: colors.successSoft, text: colors.successDark },     // Confirmed
    2: { bg: colors.primarySoft, text: colors.primaryDark },     // InProgress
    3: { bg: colors.successSoft, text: colors.successDark },     // Completed
    4: { bg: colors.dangerSoft, text: colors.dangerDark },       // Cancelled
    5: { bg: '#F1F5F9', text: colors.mediumGray },               // Expired
    6: { bg: colors.accentSoft, text: colors.accentDark },       // AwaitingPayment
    7: { bg: colors.dangerSoft, text: colors.dangerDark },       // Rejected
};

const Badge = ({ status, type = 'booking', label, style }) => {
    const labels = type === 'payment' ? PaymentStatusLabels : BookingStatusLabels;
    const displayLabel = label || labels[status] || 'Unknown';
    const colorScheme = statusColors[status] || statusColors[0];

    return (
        <View style={[styles.badge, { backgroundColor: colorScheme.bg }, style]}>
            <Text style={[styles.text, { color: colorScheme.text }]}>{displayLabel}</Text>
        </View>
    );
};

const styles = StyleSheet.create({
    badge: {
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.xs,
        borderRadius: spacing.radius.full,
        alignSelf: 'flex-start',
    },
    text: {
        ...typography.caption,
        fontWeight: typography.weight.semibold,
    },
});

export default Badge;
