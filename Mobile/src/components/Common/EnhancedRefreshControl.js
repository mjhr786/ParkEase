/**
 * EnhancedRefreshControl
 * Premium pull-to-refresh with animated icon, branded colors, and last-updated timestamp
 */

import React, { useState, useCallback, useRef, useEffect } from 'react';
import { View, Text, RefreshControl, StyleSheet, Animated, Easing } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, typography } from '../../styles/globalStyles';

const formatTimeAgo = (date) => {
    if (!date) return '';
    const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
    if (seconds < 5) return 'Updated just now';
    if (seconds < 60) return `Updated ${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `Updated ${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    return `Updated ${hours}h ago`;
};

/**
 * useEnhancedRefresh
 * Hook providing refreshing state, onRefresh handler, and last-refreshed timestamp
 */
export const useEnhancedRefresh = (fetchFn) => {
    const [refreshing, setRefreshing] = useState(false);
    const [lastRefreshed, setLastRefreshed] = useState(null);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        try {
            await fetchFn();
        } finally {
            setRefreshing(false);
            setLastRefreshed(new Date());
        }
    }, [fetchFn]);

    return { refreshing, onRefresh, lastRefreshed };
};

/**
 * EnhancedRefreshControl
 * Drop-in replacement for RefreshControl with premium feel
 */
const EnhancedRefreshControl = ({ refreshing, onRefresh, lastRefreshed, ...props }) => (
    <RefreshControl
        refreshing={refreshing}
        onRefresh={onRefresh}
        tintColor={colors.primary}
        colors={[colors.primary, colors.accent, colors.success]}
        progressBackgroundColor={colors.surface}
        title={lastRefreshed ? formatTimeAgo(lastRefreshed) : 'Pull to refresh'}
        titleColor={colors.textTertiary}
        {...props}
    />
);

/**
 * AnimatedRefreshHeader
 * Animated banner displayed below the header when refreshing or after refresh
 * Shows a rotating icon during refresh, then a success checkmark with timestamp
 */
export const AnimatedRefreshHeader = ({ refreshing, lastRefreshed }) => {
    const slideAnim = useRef(new Animated.Value(0)).current;
    const spinAnim = useRef(new Animated.Value(0)).current;
    const fadeAnim = useRef(new Animated.Value(0)).current;
    const scaleAnim = useRef(new Animated.Value(0.5)).current;
    const spinAnimation = useRef(null);

    useEffect(() => {
        if (refreshing) {
            // Slide in + spin
            Animated.parallel([
                Animated.spring(slideAnim, {
                    toValue: 1, friction: 6, tension: 40, useNativeDriver: true,
                }),
                Animated.timing(fadeAnim, {
                    toValue: 1, duration: 200, useNativeDriver: true,
                }),
                Animated.spring(scaleAnim, {
                    toValue: 1, friction: 5, tension: 80, useNativeDriver: true,
                }),
            ]).start();
            // Continuous spin
            spinAnimation.current = Animated.loop(
                Animated.timing(spinAnim, {
                    toValue: 1, duration: 800,
                    easing: Easing.linear, useNativeDriver: true,
                })
            );
            spinAnimation.current.start();
        } else {
            spinAnimation.current?.stop();
            spinAnim.setValue(0);
            if (lastRefreshed) {
                // Show success briefly
                Animated.sequence([
                    Animated.spring(scaleAnim, {
                        toValue: 1.15, friction: 3, tension: 100, useNativeDriver: true,
                    }),
                    Animated.spring(scaleAnim, {
                        toValue: 1, friction: 5, useNativeDriver: true,
                    }),
                    Animated.delay(2000),
                    Animated.parallel([
                        Animated.timing(slideAnim, {
                            toValue: 0, duration: 300, useNativeDriver: true,
                        }),
                        Animated.timing(fadeAnim, {
                            toValue: 0, duration: 300, useNativeDriver: true,
                        }),
                    ]),
                ]).start();
            } else {
                slideAnim.setValue(0);
                fadeAnim.setValue(0);
                scaleAnim.setValue(0.5);
            }
        }
    }, [refreshing, lastRefreshed]);

    const spin = spinAnim.interpolate({
        inputRange: [0, 1], outputRange: ['0deg', '360deg'],
    });

    const translateY = slideAnim.interpolate({
        inputRange: [0, 1], outputRange: [-40, 0],
    });

    return (
        <Animated.View
            style={[
                bannerStyles.container,
                {
                    opacity: fadeAnim,
                    transform: [{ translateY }, { scale: scaleAnim }],
                },
            ]}
            pointerEvents="none"
        >
            <View style={[bannerStyles.pill, refreshing ? bannerStyles.pillRefreshing : bannerStyles.pillDone]}>
                {refreshing ? (
                    <>
                        <Animated.View style={{ transform: [{ rotate: spin }] }}>
                            <Ionicons name="sync" size={14} color={colors.primary} />
                        </Animated.View>
                        <Text style={bannerStyles.textRefreshing}>Refreshing...</Text>
                    </>
                ) : (
                    <>
                        <Ionicons name="checkmark-circle" size={14} color={colors.success} />
                        <Text style={bannerStyles.textDone}>
                            {lastRefreshed ? formatTimeAgo(lastRefreshed) : 'Done'}
                        </Text>
                    </>
                )}
            </View>
        </Animated.View>
    );
};

/**
 * LastUpdatedBanner
 * Simple inline banner showing last refresh time
 */
export const LastUpdatedBanner = ({ lastRefreshed }) => {
    if (!lastRefreshed) return null;
    return (
        <View style={bannerStyles.inlineContainer}>
            <Text style={bannerStyles.inlineText}>{formatTimeAgo(lastRefreshed)}</Text>
        </View>
    );
};

const bannerStyles = StyleSheet.create({
    container: {
        alignItems: 'center',
        paddingVertical: spacing.xs,
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        zIndex: 100,
    },
    pill: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingHorizontal: spacing.base,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.12,
        shadowRadius: 8,
        elevation: 4,
    },
    pillRefreshing: {
        backgroundColor: colors.primarySoft,
        borderWidth: 1,
        borderColor: colors.primary + '30',
    },
    pillDone: {
        backgroundColor: colors.successSoft,
        borderWidth: 1,
        borderColor: colors.success + '30',
    },
    textRefreshing: {
        ...typography.caption,
        color: colors.primary,
        fontWeight: '600',
        fontSize: 12,
    },
    textDone: {
        ...typography.caption,
        color: colors.success,
        fontWeight: '600',
        fontSize: 12,
    },
    inlineContainer: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingVertical: spacing.xs,
        alignItems: 'center',
    },
    inlineText: {
        ...typography.caption,
        color: colors.textTertiary,
        fontSize: 11,
    },
});

export default EnhancedRefreshControl;
