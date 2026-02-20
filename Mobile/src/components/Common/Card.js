/**
 * Card Component
 * Elevated card with shadow and optional press handler
 */

import React from 'react';
import { View, TouchableOpacity, StyleSheet } from 'react-native';
import { colors, spacing, shadows } from '../../styles/globalStyles';

const Card = ({ children, onPress, style, ...props }) => {
    const Container = onPress ? TouchableOpacity : View;

    return (
        <Container
            onPress={onPress}
            activeOpacity={onPress ? 0.8 : 1}
            style={[styles.card, style]}
            {...props}
        >
            {children}
        </Container>
    );
};

const styles = StyleSheet.create({
    card: {
        backgroundColor: colors.surface,
        borderRadius: spacing.cardRadius,
        padding: spacing.cardPadding,
        marginBottom: spacing.cardMargin,
        ...shadows.card,
    },
});

export default Card;
