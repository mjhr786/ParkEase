/**
 * LoadingScreen Component
 * Full-screen loading indicator
 */

import React from 'react';
import { View, ActivityIndicator, Text, StyleSheet } from 'react-native';
import { colors, typography } from '../../styles/globalStyles';

const LoadingScreen = ({ message = 'Loading...' }) => (
    <View style={styles.container}>
        <ActivityIndicator size="large" color={colors.primary} />
        <Text style={styles.text}>{message}</Text>
    </View>
);

const styles = StyleSheet.create({
    container: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.background,
    },
    text: {
        ...typography.bodySmall,
        color: colors.textSecondary,
        marginTop: 16,
    },
});

export default LoadingScreen;
