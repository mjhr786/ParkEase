/**
 * MemberDashboardScreen
 * Stats cards, upcoming bookings, recent bookings
 */

import React, { useEffect, useCallback } from 'react';
import { View, Text, FlatList, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { getMemberDashboardThunk } from '../../store/slices/dashboardSlice';
import { useAuth } from '../../hooks/useAuth';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime, truncateText } from '../../utils/formatters';

const StatCard = ({ icon, label, value, color }) => (
    <View style={[statStyles.card, { borderLeftColor: color }]}>
        <Ionicons name={icon} size={24} color={color} />
        <Text style={statStyles.value}>{value}</Text>
        <Text style={statStyles.label}>{label}</Text>
    </View>
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

const MemberDashboardScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user } = useAuth();
    const { memberDashboard, loading } = useSelector((state) => state.dashboard);
    const [refreshing, setRefreshing] = React.useState(false);

    useEffect(() => {
        dispatch(getMemberDashboardThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getMemberDashboardThunk());
        setRefreshing(false);
    }, [dispatch]);

    if (loading && !memberDashboard) {
        return <LoadingScreen />;
    }

    const data = memberDashboard;

    const sections = [
        { type: 'header' },
        { type: 'stats' },
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
            case 'stats':
                return (
                    <View style={styles.statsRow}>
                        <StatCard icon="calendar" label="Total" value={data?.totalBookings || 0} color={colors.primary} />
                        <StatCard icon="time" label="Active" value={data?.activeBookings || 0} color={colors.success} />
                        <StatCard icon="wallet" label="Spent" value={formatCurrency(data?.totalSpent || 0)} color={colors.accent} />
                    </View>
                );
            case 'sectionTitle':
                return <Text style={styles.sectionTitle}>{item.title}</Text>;
            case 'booking':
                return <BookingItem booking={item.data} onPress={() => { }} />;
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
    statsRow: {
        flexDirection: 'row',
        gap: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
        marginTop: -spacing.lg,
        marginBottom: spacing.xl,
    },
    sectionTitle: {
        ...typography.h4,
        color: colors.textPrimary,
        paddingHorizontal: spacing.screenHorizontal,
        marginTop: spacing.base,
        marginBottom: spacing.md,
    },
});

export default MemberDashboardScreen;
