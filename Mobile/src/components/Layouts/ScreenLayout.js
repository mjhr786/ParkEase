/**
 * ScreenLayout Component
 * SafeAreaView wrapper for screens using react-native-safe-area-context
 */

import React from 'react';
import { View, StyleSheet, ScrollView, RefreshControl } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors } from '../../styles/globalStyles';

const ScreenLayout = ({
    children,
    scrollable = false,
    refreshing = false,
    onRefresh,
    style,
    contentStyle,
    edges,
}) => {
    const safeAreaProps = edges ? { edges } : {};

    if (scrollable) {
        return (
            <SafeAreaView style={[styles.safeArea, style]} {...safeAreaProps}>
                <ScrollView
                    style={styles.scrollView}
                    contentContainerStyle={[styles.scrollContent, contentStyle]}
                    showsVerticalScrollIndicator={false}
                    refreshControl={
                        onRefresh ? (
                            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />
                        ) : null
                    }
                >
                    {children}
                </ScrollView>
            </SafeAreaView>
        );
    }

    return (
        <SafeAreaView style={[styles.safeArea, style]} {...safeAreaProps}>
            <View style={[styles.container, contentStyle]}>{children}</View>
        </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    safeArea: {
        flex: 1,
        backgroundColor: colors.background,
    },
    container: {
        flex: 1,
    },
    scrollView: {
        flex: 1,
    },
    scrollContent: {
        flexGrow: 1,
    },
});

export default ScreenLayout;
