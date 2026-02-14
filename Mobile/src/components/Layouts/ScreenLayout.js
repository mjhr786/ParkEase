/**
 * ScreenLayout Component
 * SafeAreaView wrapper for screens
 */

import React from 'react';
import { View, SafeAreaView, StyleSheet, ScrollView, RefreshControl } from 'react-native';
import { colors } from '../../styles/globalStyles';

const ScreenLayout = ({
    children,
    scrollable = false,
    refreshing = false,
    onRefresh,
    style,
    contentStyle,
}) => {
    if (scrollable) {
        return (
            <SafeAreaView style={[styles.safeArea, style]}>
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
        <SafeAreaView style={[styles.safeArea, style]}>
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
