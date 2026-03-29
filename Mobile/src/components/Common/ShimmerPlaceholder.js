/**
 * ShimmerPlaceholder
 * Animated skeleton loading components to replace spinners everywhere
 * Uses looping opacity animation with a subtle gradient-like effect
 */

import React, { useEffect, useRef } from 'react';
import { View, Animated, StyleSheet } from 'react-native';
import { colors, spacing } from '../../styles/globalStyles';

// ── Base Shimmer Box ──
const ShimmerBox = ({ width, height, borderRadius = spacing.radius.md, style }) => {
    const opacity = useRef(new Animated.Value(0.3)).current;

    useEffect(() => {
        const animation = Animated.loop(
            Animated.sequence([
                Animated.timing(opacity, { toValue: 0.8, duration: 900, useNativeDriver: true }),
                Animated.timing(opacity, { toValue: 0.3, duration: 900, useNativeDriver: true }),
            ])
        );
        animation.start();
        return () => animation.stop();
    }, [opacity]);

    return (
        <Animated.View
            style={[
                {
                    width, height, borderRadius,
                    backgroundColor: colors.borderLight,
                    opacity,
                },
                style,
            ]}
        />
    );
};

// ── Shimmer Line ──
const ShimmerLine = ({ width = '100%', height = 14, style }) => (
    <ShimmerBox width={width} height={height} borderRadius={4} style={style} />
);

// ── Dashboard Skeleton ──
const DashboardSkeleton = () => (
    <View style={skeletonStyles.container}>
        {/* Hero bar */}
        <ShimmerBox width="100%" height={120} borderRadius={spacing.radius.xl} />
        {/* Quick-action tiles */}
        <View style={skeletonStyles.tilesRow}>
            {[1, 2, 3, 4, 5].map((i) => (
                <View key={i} style={skeletonStyles.tile}>
                    <ShimmerBox width={48} height={48} borderRadius={14} />
                    <ShimmerLine width={50} height={10} style={{ marginTop: 6 }} />
                </View>
            ))}
        </View>
        {/* Stats */}
        <View style={skeletonStyles.statsRow}>
            {[1, 2, 3].map((i) => (
                <ShimmerBox key={i} width="30%" height={80} borderRadius={spacing.radius.md} style={{ flex: 1 }} />
            ))}
        </View>
        {/* Cards */}
        {[1, 2, 3].map((i) => (
            <ListItemSkeleton key={i} />
        ))}
    </View>
);

// ── List Item Skeleton ──
const ListItemSkeleton = () => (
    <View style={skeletonStyles.card}>
        <View style={skeletonStyles.cardRow}>
            <View style={{ flex: 1 }}>
                <ShimmerLine width="70%" height={16} />
                <ShimmerLine width="50%" height={12} style={{ marginTop: 8 }} />
                <ShimmerLine width="40%" height={12} style={{ marginTop: 6 }} />
            </View>
            <View style={{ alignItems: 'flex-end' }}>
                <ShimmerBox width={60} height={22} borderRadius={spacing.radius.full} />
                <ShimmerLine width={50} height={14} style={{ marginTop: 8 }} />
            </View>
        </View>
    </View>
);

// ── Detail Skeleton (Parking / Booking Detail) ──
const DetailSkeleton = () => (
    <View style={skeletonStyles.detailContainer}>
        <ShimmerBox width="100%" height={220} borderRadius={0} />
        <View style={{ padding: spacing.screenHorizontal }}>
            <ShimmerLine width="80%" height={24} style={{ marginTop: spacing.lg }} />
            <ShimmerLine width="60%" height={14} style={{ marginTop: spacing.sm }} />
            <ShimmerLine width="40%" height={14} style={{ marginTop: spacing.xs }} />
            {/* Quick info row */}
            <View style={[skeletonStyles.statsRow, { marginTop: spacing.lg }]}>
                {[1, 2, 3].map((i) => (
                    <ShimmerBox key={i} width="30%" height={60} borderRadius={spacing.radius.lg} style={{ flex: 1 }} />
                ))}
            </View>
            {/* Content blocks */}
            <ShimmerBox width="100%" height={80} style={{ marginTop: spacing.lg }} />
            <ShimmerBox width="100%" height={100} style={{ marginTop: spacing.md }} />
            {/* Action buttons */}
            <View style={[skeletonStyles.statsRow, { marginTop: spacing.lg }]}>
                <ShimmerBox width="48%" height={48} borderRadius={spacing.radius.lg} style={{ flex: 1 }} />
                <ShimmerBox width="48%" height={48} borderRadius={spacing.radius.lg} style={{ flex: 1 }} />
            </View>
        </View>
    </View>
);

// ── Booking Card Skeleton ──
const BookingCardSkeleton = ({ count = 3 }) => (
    <View style={skeletonStyles.container}>
        {/* Filter chips row */}
        <View style={[skeletonStyles.chipRow, { marginBottom: spacing.md }]}>
            {[1, 2, 3, 4].map((i) => (
                <ShimmerBox key={i} width={65} height={30} borderRadius={spacing.radius.full} />
            ))}
        </View>
        {Array.from({ length: count }).map((_, i) => (
            <ListItemSkeleton key={i} />
        ))}
    </View>
);

// ── Search Skeleton ──
const SearchSkeleton = () => (
    <View style={skeletonStyles.container}>
        {/* Search bar */}
        <ShimmerBox width="100%" height={48} borderRadius={spacing.radius.lg} />
        {/* Filter row */}
        <View style={[skeletonStyles.chipRow, { marginTop: spacing.md }]}>
            {[1, 2, 3, 4].map((i) => (
                <ShimmerBox key={i} width={70} height={32} borderRadius={spacing.radius.full} />
            ))}
        </View>
        {/* Results */}
        {[1, 2, 3, 4].map((i) => (
            <View key={i} style={skeletonStyles.card}>
                <View style={skeletonStyles.cardRow}>
                    <ShimmerBox width={80} height={80} borderRadius={spacing.radius.md} style={{ marginRight: spacing.md }} />
                    <View style={{ flex: 1 }}>
                        <ShimmerLine width="75%" height={16} />
                        <ShimmerLine width="55%" height={12} style={{ marginTop: 6 }} />
                        <ShimmerLine width="35%" height={12} style={{ marginTop: 6 }} />
                        <ShimmerLine width="25%" height={16} style={{ marginTop: 8 }} />
                    </View>
                </View>
            </View>
        ))}
    </View>
);

// ── Favorites Skeleton ──
const FavoritesSkeleton = () => (
    <View style={skeletonStyles.container}>
        {[1, 2, 3, 4].map((i) => (
            <View key={i} style={skeletonStyles.card}>
                <View style={skeletonStyles.cardRow}>
                    <ShimmerBox width={64} height={64} borderRadius={spacing.radius.md} style={{ marginRight: spacing.md }} />
                    <View style={{ flex: 1 }}>
                        <ShimmerLine width="70%" height={16} />
                        <ShimmerLine width="50%" height={12} style={{ marginTop: 6 }} />
                        <ShimmerLine width="30%" height={14} style={{ marginTop: 8 }} />
                    </View>
                    <ShimmerBox width={32} height={32} borderRadius={16} />
                </View>
            </View>
        ))}
    </View>
);

// ── Vehicles Skeleton ──
const VehiclesSkeleton = () => (
    <View style={skeletonStyles.container}>
        {[1, 2, 3].map((i) => (
            <View key={i} style={[skeletonStyles.card, { padding: spacing.lg }]}>
                <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: spacing.md }}>
                    <ShimmerBox width={48} height={48} borderRadius={24} style={{ marginRight: spacing.md }} />
                    <View style={{ flex: 1 }}>
                        <ShimmerLine width="60%" height={16} />
                        <ShimmerLine width="40%" height={12} style={{ marginTop: 6 }} />
                    </View>
                </View>
                <View style={skeletonStyles.chipRow}>
                    <ShimmerBox width={70} height={24} borderRadius={spacing.radius.full} />
                    <ShimmerBox width={80} height={24} borderRadius={spacing.radius.full} />
                </View>
            </View>
        ))}
    </View>
);

// ── Notifications Skeleton ──
const NotificationsSkeleton = () => (
    <View style={skeletonStyles.container}>
        {[1, 2, 3, 4, 5].map((i) => (
            <View key={i} style={[skeletonStyles.card, { flexDirection: 'row', alignItems: 'center' }]}>
                <ShimmerBox width={40} height={40} borderRadius={20} style={{ marginRight: spacing.md }} />
                <View style={{ flex: 1 }}>
                    <ShimmerLine width="80%" height={14} />
                    <ShimmerLine width="60%" height={11} style={{ marginTop: 6 }} />
                </View>
                <ShimmerBox width={40} height={12} borderRadius={4} />
            </View>
        ))}
    </View>
);

// ── Chat List Skeleton ──
const ChatListSkeleton = () => (
    <View style={skeletonStyles.container}>
        {[1, 2, 3, 4, 5].map((i) => (
            <View key={i} style={[skeletonStyles.card, { flexDirection: 'row', alignItems: 'center' }]}>
                <ShimmerBox width={50} height={50} borderRadius={25} style={{ marginRight: spacing.md }} />
                <View style={{ flex: 1 }}>
                    <ShimmerLine width="55%" height={15} />
                    <ShimmerLine width="80%" height={11} style={{ marginTop: 6 }} />
                </View>
                <View style={{ alignItems: 'flex-end' }}>
                    <ShimmerLine width={35} height={10} />
                    <ShimmerBox width={20} height={20} borderRadius={10} style={{ marginTop: 6 }} />
                </View>
            </View>
        ))}
    </View>
);

// ── Reviews Skeleton ──
const ReviewsSkeleton = () => (
    <View style={skeletonStyles.container}>
        {/* Average rating */}
        <View style={[skeletonStyles.card, { alignItems: 'center', paddingVertical: spacing.xl }]}>
            <ShimmerBox width={60} height={36} borderRadius={4} />
            <ShimmerBox width={100} height={16} borderRadius={4} style={{ marginTop: spacing.sm }} />
            <ShimmerLine width={80} height={12} style={{ marginTop: 6 }} />
        </View>
        {/* Reviews */}
        {[1, 2, 3].map((i) => (
            <View key={i} style={skeletonStyles.card}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: spacing.sm }}>
                    <ShimmerLine width="40%" height={14} />
                    <ShimmerBox width={80} height={14} borderRadius={4} />
                </View>
                <ShimmerLine width="90%" height={12} />
                <ShimmerLine width="70%" height={12} style={{ marginTop: 4 }} />
                <ShimmerLine width={60} height={10} style={{ marginTop: 8 }} />
            </View>
        ))}
    </View>
);

const skeletonStyles = StyleSheet.create({
    container: { padding: spacing.screenHorizontal },
    detailContainer: {},
    tilesRow: {
        flexDirection: 'row', justifyContent: 'space-around',
        paddingVertical: spacing.lg,
    },
    tile: { alignItems: 'center' },
    statsRow: {
        flexDirection: 'row', gap: spacing.md,
        marginBottom: spacing.lg,
    },
    chipRow: {
        flexDirection: 'row', gap: spacing.sm,
    },
    card: {
        backgroundColor: colors.surface,
        borderRadius: spacing.radius.md,
        padding: spacing.md,
        marginBottom: spacing.md,
    },
    cardRow: { flexDirection: 'row', alignItems: 'center' },
});

export {
    ShimmerBox,
    ShimmerLine,
    DashboardSkeleton,
    ListItemSkeleton,
    DetailSkeleton,
    BookingCardSkeleton,
    SearchSkeleton,
    FavoritesSkeleton,
    VehiclesSkeleton,
    NotificationsSkeleton,
    ChatListSkeleton,
    ReviewsSkeleton,
};
