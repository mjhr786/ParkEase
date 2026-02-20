/**
 * VendorDashboardScreen
 * Vendor stats, earnings summary, recent bookings
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { getVendorDashboardThunk } from '../../store/slices/dashboardSlice';
import { useAuth } from '../../hooks/useAuth';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import LoadingScreen from '../../components/Common/LoadingScreen';
import EmptyState from '../../components/Common/EmptyState';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';

const StatCard = ({ icon, label, value, color, bg }) => (
    <View style={[vStatStyles.card, { backgroundColor: bg }]}>
        <View style={[vStatStyles.iconWrap, { backgroundColor: color }]}>
            <Ionicons name={icon} size={20} color={colors.white} />
        </View>
        <Text style={vStatStyles.value}>{value}</Text>
        <Text style={vStatStyles.label}>{label}</Text>
    </View>
);

const vStatStyles = StyleSheet.create({
    card: { flex: 1, borderRadius: spacing.radius.lg, padding: spacing.md, alignItems: 'center' },
    iconWrap: { width: 40, height: 40, borderRadius: 20, justifyContent: 'center', alignItems: 'center', marginBottom: spacing.sm },
    value: { ...typography.h3, color: colors.textPrimary },
    label: { ...typography.caption, color: colors.textTertiary, marginTop: 2, textAlign: 'center' },
});

const VendorDashboardScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user } = useAuth();
    const { vendorDashboard: data, loading } = useSelector((s) => s.dashboard);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getVendorDashboardThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getVendorDashboardThunk());
        setRefreshing(false);
    }, [dispatch]);

    if (loading && !data) return <LoadingScreen />;

    const sections = [
        { type: 'header' },
        { type: 'stats' },
        { type: 'earnings' },
        ...(data?.recentBookings?.length ? [{ type: 'sectionTitle', title: 'Recent Bookings' }] : []),
        ...(data?.recentBookings || []).map((b) => ({ type: 'booking', data: b })),
        ...(!data?.recentBookings?.length ? [{ type: 'empty' }] : []),
    ];

    const renderItem = ({ item }) => {
        switch (item.type) {
            case 'header':
                return (
                    <LinearGradient colors={colors.gradients.dark} style={styles.hero}>
                        <Text style={styles.greeting}>Welcome, {user?.firstName} 👋</Text>
                        <Text style={styles.heroSub}>Manage your parking business</Text>
                    </LinearGradient>
                );
            case 'stats':
                return (
                    <View style={styles.statsRow}>
                        <StatCard icon="location" label="Spaces" value={data?.totalParkingSpaces || 0} color={colors.primary} bg={colors.primarySoft} />
                        <StatCard icon="calendar" label="Bookings" value={data?.totalBookings || 0} color={colors.success} bg={colors.successSoft} />
                        <StatCard icon="wallet" label="Earnings" value={formatCurrency(data?.totalEarnings || 0)} color={colors.accent} bg={colors.accentSoft} />
                    </View>
                );
            case 'earnings':
                return (
                    <Card style={styles.earningsCard}>
                        <Text style={styles.sectionTitle}>This Month</Text>
                        <Text style={styles.earningsValue}>{formatCurrency(data?.monthlyEarnings || 0)}</Text>
                        <Text style={styles.earningsLabel}>Revenue</Text>
                    </Card>
                );
            case 'sectionTitle':
                return <Text style={styles.sectionHeader}>{item.title}</Text>;
            case 'booking':
                return (
                    <Card style={styles.bookingCard}>
                        <View style={styles.bookingRow}>
                            <View style={{ flex: 1 }}>
                                <Text style={styles.bookingTitle}>{item.data.userName}</Text>
                                <Text style={styles.bookingMeta}>{item.data.parkingSpaceTitle}</Text>
                                <Text style={styles.bookingTime}>{formatDate(item.data.startDateTime)} · {formatTime(item.data.startDateTime)}</Text>
                            </View>
                            <View style={{ alignItems: 'flex-end', gap: 4 }}>
                                <Badge status={item.data.status} />
                                <Text style={styles.bookingAmount}>{formatCurrency(item.data.totalAmount)}</Text>
                            </View>
                        </View>
                    </Card>
                );
            case 'empty':
                return <EmptyState icon="analytics-outline" title="No recent bookings" message="Your booking activity will appear here" />;
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
    hero: { paddingTop: 60, paddingBottom: spacing['2xl'], paddingHorizontal: spacing.screenHorizontal, borderBottomLeftRadius: spacing.radius.xl, borderBottomRightRadius: spacing.radius.xl },
    greeting: { fontSize: 28, fontWeight: '700', color: colors.white },
    heroSub: { ...typography.body, color: 'rgba(255,255,255,0.7)', marginTop: spacing.xs },
    statsRow: { flexDirection: 'row', gap: spacing.md, paddingHorizontal: spacing.screenHorizontal, marginTop: -spacing.lg },
    earningsCard: { marginHorizontal: spacing.screenHorizontal, alignItems: 'center', paddingVertical: spacing.xl, backgroundColor: colors.accentSoft },
    sectionTitle: { ...typography.label, color: colors.textSecondary, marginBottom: spacing.xs },
    earningsValue: { ...typography.h1, color: colors.accentDark },
    earningsLabel: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    sectionHeader: { ...typography.h4, color: colors.textPrimary, paddingHorizontal: spacing.screenHorizontal, marginTop: spacing.base, marginBottom: spacing.md },
    bookingCard: { marginHorizontal: spacing.screenHorizontal },
    bookingRow: { flexDirection: 'row', alignItems: 'center' },
    bookingTitle: { ...typography.label, color: colors.textPrimary },
    bookingMeta: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    bookingTime: { ...typography.caption, color: colors.textSecondary, marginTop: 4 },
    bookingAmount: { ...typography.label, color: colors.primary },
});

export default VendorDashboardScreen;
