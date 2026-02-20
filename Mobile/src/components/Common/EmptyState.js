/**
 * EmptyState Component
 * Placeholder for empty lists/data
 */

import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, typography } from '../../styles/globalStyles';
import Button from './Button';

const EmptyState = ({ icon = 'folder-open-outline', title, message, actionLabel, onAction }) => (
    <View style={styles.container}>
        <Ionicons name={icon} size={64} color={colors.lightGray} />
        <Text style={styles.title}>{title || 'Nothing here yet'}</Text>
        {message && <Text style={styles.message}>{message}</Text>}
        {actionLabel && onAction && (
            <Button title={actionLabel} onPress={onAction} variant="secondary" size="sm" style={{ marginTop: spacing.base }} />
        )}
    </View>
);

const styles = StyleSheet.create({
    container: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        paddingHorizontal: spacing.xl,
        paddingVertical: spacing['3xl'],
    },
    title: {
        ...typography.h4,
        color: colors.textSecondary,
        marginTop: spacing.base,
        textAlign: 'center',
    },
    message: {
        ...typography.bodySmall,
        color: colors.textTertiary,
        textAlign: 'center',
        marginTop: spacing.sm,
        lineHeight: 20,
    },
});

export default EmptyState;
