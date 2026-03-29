/**
 * UnifiedDashboardScreen
 * Combined dashboard — stats, quick tiles, favorites, recent activity
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { getMemberDashboardThunk, getVendorDashboardThunk } from '../../store/slices/dashboardSlice';
import { getFavoritesThunk } from '../../store/slices/favoriteSlice';
import { useAuth } from '../../hooks/useAuth';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import EmptyState from '../../components/Common/EmptyState';
import { DashboardSkeleton } from '../../components/Common/ShimmerPlaceholder';
import EnhancedRefreshControl, { useEnhancedRefresh } from '../../components/Common/EnhancedRefreshControl';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';

/* ───── Quick-Action Tile ───── */
const QuickTile = ({ icon, label, color, onPress }) => (
    <TouchableOpacity style={tileStyles.tile} onPress={onPress} activeOpacity={0.7}>
        <View style={[tileStyles.iconWrap, { backgroundColor: color + '18' }]}>
            <Ionicons name={icon} size={22} color={color} />
        </View>
        <Text style={tileStyles.label} numberOfLines={1}>{label}</Text>
    </TouchableOpacity>
);

const tileStyles = StyleSheet.create({
    tile: { alignItems: 'center', width: 72 },
    iconWrap: {
        width: 48, height: 48, borderRadius: 14,
        justifyContent: 'center', alignItems: 'center', marginBottom: 6,
    },
    label: { ...typography.caption, color: colors.textSecondary, fontWeight: '500', textAlign: 'center' },
});

/* ───── Stat Card ───── */
const StatCard = ({ icon, label, value, color, onPress }) => (
    <TouchableOpacity style={[statStyles.card, { borderLeftColor: color }]} onPress={onPress} activeOpacity={0.7}>
        <Ionicons name={icon} size={24} color={color} />
        <Text style={statStyles.value}>{value}</Text>
        <Text style={statStyles.label}>{label}</Text>
    </TouchableOpacity>
);

const statStyles = StyleSheet.create({
    card: {
        flex: 1, backgroundColor: colors.surface, borderRadius: spacing.radius.md,
        padding: spacing.md, borderLeftWidth: 3, ...shadows.sm,
    },
    value: { ...typography.h3, color: colors.textPrimary, marginTop: spacing.xs },
    label: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
});

/* ───── Favorite Card (horizontal) ───── */
const FavoriteCard = ({ item, onPress }) => (
    <TouchableOpacity style={favStyles.card} onPress={onPress} activeOpacity={0.7}>
        <View style={favStyles.imagePlaceholder}>
            <Ionicons name="location" size={24} color={colors.primary} />
        </View>
        <Text style={favStyles.title} numberOfLines={1}>{item.title || item.parkingSpaceTitle}</Text>
        <Text style={favStyles.price}>
            {item.pricePerHour != null ? `${formatCurrency(item.pricePerHour)}/hr` : ''}
        </Text>
    </TouchableOpacity>
);

const favStyles = StyleSheet.create({
    card: {
        width: 140, backgroundColor: colors.surface,
        borderRadius: spacing.radius.md, overflow: 'hidden',
        marginRight: spacing.md, ...shadows.sm,
    },
    imagePlaceholder: {
        height: 80, backgroundColor: colors.primarySoft,
        justifyContent: 'center', alignItems: 'center',
    },
    title: { ...typography.caption, color: colors.textPrimary, fontWeight: '600', paddingHorizontal: 8, paddingTop: 8 },
    price: { ...typography.caption, color: colors.primary, fontWeight: '600', paddingHorizontal: 8, paddingBottom: 8, marginTop: 2 },
});

/* ───── Booking Item ───── */
const BookingItem = ({ booking, onPress }) => (
    <Card onPress={onPress} style={bookingStyles.card}>
        <View style={bookingStyles.row}>
            <View style={{ flex: 1 }}>
                <Text style={bookingStyles.title} numberOfLines={1}>{booking.parkingSpaceTitle}</Text>
                <Text style={bookingStyles.address} numberOfLines={1}>{booking.parkingSpaceAddress || booking.userName}</Text>
                <Text style={bookingStyles.time}>
                    {formatDate(booking.startDateTime)} · {formatTime(booking.startDateTime)}
                </Text>
            </View>
            <View style={bookingStyles.right}>
                <Badge status={booking.status} />
                <Text style={bookingStyles.amount}>{formatCurrency(booking.totalAmount)}</Text>
            </View>
        </View>
    </Card>
);

const bookingStyles = StyleSheet.create({
    card: { marginHorizontal: spacing.screenHorizontal },
    row: { flexDirection: 'row', alignItems: 'center' },
    title: { ...typography.label, color: colors.textPrimary },
    address: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    time: { ...typography.caption, color: colors.textSecondary, marginTop: 4 },
    right: { alignItems: 'flex-end', gap: spacing.xs },
    amount: { ...typography.label, color: colors.textPrimary },
});

/* ───── Main Screen ───── */
const UnifiedDashboardScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user } = useAuth();
    const { memberDashboard, vendorDashboard, loading } = useSelector((state) => state.dashboard);
    const { favorites } = useSelector((state) => state.favorite);
    const insets = useSafeAreaInsets();

    useEffect(() => {
        dispatch(getMemberDashboardThunk());
        dispatch(getVendorDashboardThunk());
        dispatch(getFavoritesThunk());
    }, [dispatch]);

    const fetchAll = useCallback(async () => {
        await Promise.all([
            dispatch(getMemberDashboardThunk()),
            dispatch(getVendorDashboardThunk()),
            dispatch(getFavoritesThunk()),
        ]);
    }, [dispatch]);

    const { refreshing, onRefresh, lastRefreshed } = useEnhancedRefresh(fetchAll);

    const navigateToBookingDetail = useCallback((bookingId) => {
        navigation.navigate('BookingDetail', { bookingId });
    }, [navigation]);

    const navigateToFavoriteDetail = useCallback((parkingSpaceId) => {
        navigation.navigate('ParkingDetail', { parkingId: parkingSpaceId });
    }, [navigation]);

    if (loading && !memberDashboard && !vendorDashboard) {
        return <DashboardSkeleton />;
    }

    const memberData = memberDashboard;
    const vendorData = vendorDashboard;

    const sections = [
        { type: 'header' },
        { type: 'tiles' },
        { type: 'stats' },
        // Earnings card (from vendor data)
        ...(vendorData?.totalEarnings ? [{ type: 'earnings' }] : []),
        // Favorites
        ...(favorites?.length ? [{ type: 'favoritesHeader' }, { type: 'favorites' }] : []),
        // Upcoming bookings (as a booker)
        ...(memberData?.upcomingBookings?.length ? [{ type: 'sectionTitle', title: 'Upcoming Bookings' }] : []),
        ...(memberData?.upcomingBookings || []).map((b) => ({ type: 'booking', data: b })),
        // Recent activity
        ...(memberData?.recentBookings?.length ? [{ type: 'sectionTitle', title: 'Recent Activity' }] : []),
        ...(memberData?.recentBookings || []).map((b) => ({ type: 'booking', data: b })),
        // Empty state
        ...(!memberData?.upcomingBookings?.length && !memberData?.recentBookings?.length ? [{ type: 'empty' }] : []),
    ];

    const renderItem = ({ item }) => {
        switch (item.type) {
            case 'header':
                return (
                    <LinearGradient colors={colors.gradients.hero} style={[styles.heroGradient, { paddingTop: insets.top + spacing.md }]}>
                        <View style={styles.heroContent}>
                            <Text style={styles.greeting}>Hello, {user?.firstName || 'there'} 👋</Text>
                            <Text style={styles.heroSubtitle}>Park, list, and earn — all in one place</Text>
                        </View>
                    </LinearGradient>
                );
            case 'tiles':
                return (
                    <View style={styles.tilesRow}>
                        <QuickTile icon="search" label="Search" color={colors.primary}
                            onPress={() => navigation.navigate('SearchTab')} />
                        <QuickTile icon="list" label="Listings" color={colors.success}
                            onPress={() => navigation.navigate('ListingsTab')} />
                        <QuickTile icon="calendar" label="Bookings" color={colors.accent}
                            onPress={() => navigation.navigate('BookingsTab')} />
                        <QuickTile icon="heart" label="Favorites" color={colors.danger}
                            onPress={() => navigation.navigate('Favorites')} />
                        <QuickTile icon="car-sport" label="Garage" color="#6C63FF"
                            onPress={() => navigation.navigate('Vehicles')} />
                    </View>
                );
            case 'stats':
                return (
                    <View style={styles.statsRow}>
                        <StatCard icon="location" label="My Spaces" value={vendorData?.totalParkingSpaces || 0} color={colors.primary}
                            onPress={() => navigation.navigate('ListingsTab')} />
                        <StatCard icon="calendar" label="Bookings" value={memberData?.totalBookings || 0} color={colors.success}
                            onPress={() => navigation.navigate('BookingsTab')} />
                        <StatCard icon="wallet" label="Earnings" value={formatCurrency(vendorData?.totalEarnings || 0)} color={colors.accent} />
                    </View>
                );
            case 'earnings':
                return (
                    <Card style={styles.earningsCard}>
                        <Text style={styles.earningsLabel}>This Month</Text>
                        <Text style={styles.earningsValue}>{formatCurrency(vendorData?.monthlyEarnings || 0)}</Text>
                        <Text style={styles.earningsSub}>Revenue from your listings</Text>
                    </Card>
                );
            case 'favoritesHeader':
                return (
                    <View style={styles.sectionRow}>
                        <Text style={styles.sectionTitleInline}>Favorites</Text>
                        <TouchableOpacity onPress={() => navigation.navigate('Favorites')}>
                            <Text style={styles.seeAll}>See All</Text>
                        </TouchableOpacity>
                    </View>
                );
            case 'favorites':
                return (
                    <FlatList
                        data={favorites.slice(0, 10)}
                        horizontal
                        showsHorizontalScrollIndicator={false}
                        keyExtractor={(f) => (f.parkingSpaceId || f.id)?.toString()}
                        renderItem={({ item: fav }) => (
                            <FavoriteCard
                                item={fav}
                                onPress={() => navigateToFavoriteDetail(fav.parkingSpaceId || fav.id)}
                            />
                        )}
                        contentContainerStyle={{ paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.md }}
                    />
                );
            case 'sectionTitle':
                return <Text style={styles.sectionTitle}>{item.title}</Text>;
            case 'booking':
                return <BookingItem booking={item.data} onPress={() => navigateToBookingDetail(item.data.id)} />;
            case 'empty':
                return <EmptyState icon="car-outline" title="No bookings yet" message="Search for parking or list your own space!" />;
            default:
                return null;
        }
    };

    return (
        <ScreenLayout edges={['bottom']}>
            <FlatList
                data={sections}
                renderItem={renderItem}
                keyExtractor={(item, index) => `${item.type}-${index}`}
                showsVerticalScrollIndicator={false}
                refreshControl={<EnhancedRefreshControl refreshing={refreshing} onRefresh={onRefresh} lastRefreshed={lastRefreshed} />}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    heroGradient: {
        paddingBottom: spacing['2xl'],
        paddingHorizontal: spacing.screenHorizontal,
        borderBottomLeftRadius: spacing.radius.xl,
        borderBottomRightRadius: spacing.radius.xl,
    },
    heroContent: {},
    greeting: { fontSize: 28, fontWeight: '700', color: colors.white },
    heroSubtitle: { ...typography.body, color: 'rgba(255,255,255,0.8)', marginTop: spacing.xs },
    tilesRow: {
        flexDirection: 'row', justifyContent: 'space-around',
        paddingHorizontal: spacing.screenHorizontal, paddingVertical: spacing.lg,
    },
    statsRow: {
        flexDirection: 'row', gap: spacing.md,
        paddingHorizontal: spacing.screenHorizontal, marginBottom: spacing.lg,
    },
    earningsCard: { marginHorizontal: spacing.screenHorizontal, alignItems: 'center', paddingVertical: spacing.xl, backgroundColor: colors.accentSoft },
    earningsLabel: { ...typography.label, color: colors.textSecondary, marginBottom: spacing.xs },
    earningsValue: { ...typography.h1, color: colors.accentDark },
    earningsSub: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    sectionRow: {
        flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
        paddingHorizontal: spacing.screenHorizontal, marginTop: spacing.base, marginBottom: spacing.md,
    },
    sectionTitleInline: { ...typography.h4, color: colors.textPrimary },
    sectionTitle: {
        ...typography.h4, color: colors.textPrimary,
        paddingHorizontal: spacing.screenHorizontal, marginTop: spacing.base, marginBottom: spacing.md,
    },
    seeAll: { ...typography.caption, color: colors.primary, fontWeight: '600' },
});

export default UnifiedDashboardScreen;
