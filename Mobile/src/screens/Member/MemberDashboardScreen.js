/**
 * MemberDashboardScreen
 * Stats cards, quick-action tiles, favorites, upcoming bookings, recent bookings
 */

import React, { useEffect, useCallback } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { getMemberDashboardThunk } from '../../store/slices/dashboardSlice';
import { getFavoritesThunk } from '../../store/slices/favoriteSlice';
import { useAuth } from '../../hooks/useAuth';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { ParkingType } from '../../utils/constants';

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
        flex: 1,
        backgroundColor: colors.surface,
        borderRadius: spacing.radius.md,
        padding: spacing.md,
        borderLeftWidth: 3,
        ...shadows.sm,
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
                <Text style={bookingStyles.address} numberOfLines={1}>{booking.parkingSpaceAddress}</Text>
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
const MemberDashboardScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user } = useAuth();
    const { memberDashboard, loading } = useSelector((state) => state.dashboard);
    const { favorites } = useSelector((state) => state.favorite);
    const [refreshing, setRefreshing] = React.useState(false);

    useEffect(() => {
        dispatch(getMemberDashboardThunk());
        dispatch(getFavoritesThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await Promise.all([
            dispatch(getMemberDashboardThunk()),
            dispatch(getFavoritesThunk()),
        ]);
        setRefreshing(false);
    }, [dispatch]);

    const navigateToBookingDetail = useCallback((bookingId) => {
        navigation.navigate('BookingDetail', { bookingId });
    }, [navigation]);

    const navigateToFavoriteDetail = useCallback((parkingSpaceId) => {
        navigation.navigate('ParkingDetail', { parkingId: parkingSpaceId });
    }, [navigation]);

    if (loading && !memberDashboard) {
        return <LoadingScreen />;
    }

    const data = memberDashboard;

    const sections = [
        { type: 'header' },
        { type: 'tiles' },
        { type: 'stats' },
        ...(favorites?.length ? [{ type: 'favoritesHeader' }, { type: 'favorites' }] : []),
        ...(data?.upcomingBookings?.length ? [{ type: 'sectionTitle', title: 'Upcoming Bookings' }] : []),
        ...(data?.upcomingBookings || []).map((b) => ({ type: 'booking', data: b })),
        ...(data?.recentBookings?.length ? [{ type: 'sectionTitle', title: 'Recent Bookings' }] : []),
        ...(data?.recentBookings || []).map((b) => ({ type: 'booking', data: b })),
        ...(!data?.upcomingBookings?.length && !data?.recentBookings?.length ? [{ type: 'empty' }] : []),
    ];

    const renderItem = ({ item }) => {
        switch (item.type) {
            case 'header':
                return (
                    <LinearGradient colors={colors.gradients.hero} style={styles.heroGradient}>
                        <View style={styles.heroContent}>
                            <Text style={styles.greeting}>Hello, {user?.firstName || 'there'} 👋</Text>
                            <Text style={styles.heroSubtitle}>Find your perfect parking spot</Text>
                        </View>
                    </LinearGradient>
                );
            case 'tiles':
                return (
                    <View style={styles.tilesRow}>
                        <QuickTile icon="search" label="Search" color={colors.primary}
                            onPress={() => navigation.navigate('SearchTab')} />
                        <QuickTile icon="calendar" label="Bookings" color={colors.success}
                            onPress={() => navigation.navigate('BookingsTab')} />
                        <QuickTile icon="heart" label="Favorites" color={colors.danger}
                            onPress={() => navigation.navigate('Favorites')} />
                        <QuickTile icon="notifications" label="Alerts" color={colors.accent}
                            onPress={() => navigation.navigate('Notifications')} />
                        <QuickTile icon="car-sport" label="My Vehicles" color="#6C63FF"
                            onPress={() => navigation.navigate('Vehicles')} />
                    </View>
                );
            case 'stats':
                return (
                    <View style={styles.statsRow}>
                        <StatCard icon="calendar" label="Total" value={data?.totalBookings || 0} color={colors.primary}
                            onPress={() => navigation.navigate('BookingsTab')} />
                        <StatCard icon="time" label="Active" value={data?.activeBookings || 0} color={colors.success}
                            onPress={() => navigation.navigate('BookingsTab')} />
                        <StatCard icon="wallet" label="Spent" value={formatCurrency(data?.totalSpent || 0)} color={colors.accent}
                            onPress={() => navigation.navigate('BookingsTab')} />
                    </View>
                );
            case 'favoritesHeader':
                return (
                    <View style={styles.sectionRow}>
                        <Text style={styles.sectionTitle}>Favorites</Text>
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
                return <EmptyState icon="car-outline" title="No bookings yet" message="Search for parking spaces and book your first spot!" />;
            default:
                return null;
        }
    };

    return (
        <ScreenLayout>
            <FlatList
                data={sections}
                renderItem={renderItem}
                keyExtractor={(item, index) => `${item.type}-${index}`}
                showsVerticalScrollIndicator={false}
                refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    heroGradient: {
        paddingTop: 60,
        paddingBottom: spacing['2xl'],
        paddingHorizontal: spacing.screenHorizontal,
        borderBottomLeftRadius: spacing.radius.xl,
        borderBottomRightRadius: spacing.radius.xl,
    },
    heroContent: {},
    greeting: {
        fontSize: 28,
        fontWeight: '700',
        color: colors.white,
    },
    heroSubtitle: {
        ...typography.body,
        color: 'rgba(255,255,255,0.8)',
        marginTop: spacing.xs,
    },
    tilesRow: {
        flexDirection: 'row',
        justifyContent: 'space-around',
        paddingHorizontal: spacing.screenHorizontal,
        paddingVertical: spacing.lg,
    },
    statsRow: {
        flexDirection: 'row',
        gap: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
        marginBottom: spacing.lg,
    },
    sectionRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingHorizontal: spacing.screenHorizontal,
        marginTop: spacing.base,
        marginBottom: spacing.md,
    },
    sectionTitle: {
        ...typography.h4,
        color: colors.textPrimary,
        paddingHorizontal: spacing.screenHorizontal,
        marginTop: spacing.base,
        marginBottom: spacing.md,
    },
    seeAll: {
        ...typography.caption,
        color: colors.primary,
        fontWeight: '600',
    },
});

export default MemberDashboardScreen;
