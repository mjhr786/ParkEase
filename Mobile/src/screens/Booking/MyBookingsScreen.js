/**
 * MyBookingsScreen
 * User's bookings list with status filter tabs
 */

import React, { useEffect, useCallback, useState, useMemo } from 'react';
import {
    View,
    Text,
    FlatList,
    TouchableOpacity,
    StyleSheet,
    RefreshControl,
    TextInput,
    ScrollView,
    PanResponder,
} from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getMyBookingsThunk } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import EmptyState from '../../components/Common/EmptyState';
import { BookingCardSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { BookingStatus, BookingStatusLabels } from '../../utils/constants';

const FILTERS = [
    { label: 'All', value: null },
    { label: 'Active', value: [BookingStatus.Confirmed, BookingStatus.InProgress] },
    { label: 'Completed', value: [BookingStatus.Completed] },
    { label: 'Cancelled', value: [BookingStatus.Cancelled, BookingStatus.Rejected] },
];

const getStatusTone = (status) => {
    switch (status) {
        case BookingStatus.Confirmed:
            return {
                backgroundColor: colors.successSoft,
                borderColor: colors.successLight,
                textColor: colors.successDark,
                icon: 'checkmark-circle',
            };
        case BookingStatus.InProgress:
            return {
                backgroundColor: colors.primarySoft,
                borderColor: colors.primaryLight,
                textColor: colors.primaryDark,
                icon: 'play-circle',
            };
        case BookingStatus.Completed:
            return {
                backgroundColor: colors.infoSoft,
                borderColor: colors.info,
                textColor: colors.infoDark,
                icon: 'flag',
            };
        case BookingStatus.Cancelled:
        case BookingStatus.Rejected:
            return {
                backgroundColor: colors.dangerSoft,
                borderColor: colors.dangerLight,
                textColor: colors.dangerDark,
                icon: 'close-circle',
            };
        default:
            return {
                backgroundColor: colors.accentSoft,
                borderColor: colors.accentLight,
                textColor: colors.accentDark,
                icon: 'time',
            };
    }
};

const FilterChip = ({ label, active, onPress }) => (
    <TouchableOpacity
        style={[styles.filterChip, active && styles.filterChipActive]}
        onPress={onPress}
    >
        <Text style={[styles.filterChipText, active && styles.filterChipTextActive]}>
            {label}
        </Text>
    </TouchableOpacity>
);

const BookingCard = ({ item, onPress }) => {
    const statusTone = getStatusTone(item.status);
    const durationHours = Math.max(
        1,
        Math.round((new Date(item.endDateTime) - new Date(item.startDateTime)) / (1000 * 60 * 60))
    );

    return (
        <Card onPress={onPress} style={cardStyles.card}>
            <View style={cardStyles.heroBand}>
                <View style={cardStyles.heroGlow} />
                <View style={cardStyles.heroTopRow}>
                    <View style={cardStyles.referencePill}>
                        <Ionicons name="pricetag-outline" size={12} color={colors.white} />
                        <Text style={cardStyles.referencePillText}>{item.bookingReference}</Text>
                    </View>
                    <View style={[cardStyles.statusPill, { backgroundColor: statusTone.backgroundColor, borderColor: statusTone.borderColor }]}>
                        <Ionicons name={statusTone.icon} size={12} color={statusTone.textColor} />
                        <Text style={[cardStyles.statusPillText, { color: statusTone.textColor }]}>
                            {BookingStatusLabels[item.status] || 'Pending'}
                        </Text>
                    </View>
                </View>

                <Text style={cardStyles.bookingTitle} numberOfLines={1}>
                    {item.parkingSpaceTitle}
                </Text>
                <View style={cardStyles.locationRow}>
                    <Ionicons name="location-outline" size={14} color="rgba(255,255,255,0.82)" />
                    <Text style={cardStyles.locationText} numberOfLines={1}>
                        {item.parkingSpaceAddress || 'Address unavailable'}
                    </Text>
                </View>
            </View>

            <View style={cardStyles.infoGrid}>
                <View style={cardStyles.infoTile}>
                    <Text style={cardStyles.infoLabel}>Date</Text>
                    <Text style={cardStyles.infoValue}>{formatDate(item.startDateTime)}</Text>
                </View>
                <View style={cardStyles.infoTile}>
                    <Text style={cardStyles.infoLabel}>Duration</Text>
                    <Text style={cardStyles.infoValue}>{durationHours} hr</Text>
                </View>
                <View style={cardStyles.infoTile}>
                    <Text style={cardStyles.infoLabel}>Time</Text>
                    <Text style={cardStyles.infoValue}>
                        {formatTime(item.startDateTime)} - {formatTime(item.endDateTime)}
                    </Text>
                </View>
                <View style={cardStyles.infoTile}>
                    <Text style={cardStyles.infoLabel}>Amount</Text>
                    <Text style={cardStyles.amountValue}>{formatCurrency(item.totalAmount)}</Text>
                </View>
            </View>

            <View style={cardStyles.footer}>
                <Ionicons name="chevron-forward" size={18} color={colors.textTertiary} />
            </View>
        </Card>
    );
};

const MyBookingsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { myBookings, myBookingsLoading } = useSelector((s) => s.booking);
    const [activeFilter, setActiveFilter] = useState(0);
    const [searchQuery, setSearchQuery] = useState('');
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getMyBookingsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getMyBookingsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const filteredBookings = useMemo(() => (
        myBookings.filter((b) => {
            const filter = FILTERS[activeFilter].value;
            const matchesStatus = !filter || filter.includes(b.status);
            const query = searchQuery.trim().toLowerCase();
            const matchesSearch = !query
                || b.parkingSpaceTitle?.toLowerCase().includes(query)
                || b.parkingSpaceAddress?.toLowerCase().includes(query)
                || b.bookingReference?.toLowerCase().includes(query);

            return matchesStatus && matchesSearch;
        })
    ), [activeFilter, myBookings, searchQuery]);

    const swipeResponder = useMemo(() => PanResponder.create({
        onMoveShouldSetPanResponder: (_, gestureState) => (
            Math.abs(gestureState.dx) > 18 && Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.25
        ),
        onPanResponderRelease: (_, gestureState) => {
            if (gestureState.dx < -80) {
                navigation.replace('IncomingBookings');
            }
        },
    }), [navigation]);

    const headerContent = (
        <View style={styles.heroSection}>
            <View style={styles.topTabRow}>
                <TouchableOpacity style={[styles.topTab, styles.topTabActive]}>
                    <Text style={[styles.topTabText, styles.topTabTextActive]}>Bookings</Text>
                </TouchableOpacity>
                <TouchableOpacity
                    style={styles.topTab}
                    onPress={() => navigation.replace('IncomingBookings')}
                >
                    <Text style={styles.topTabText}>Incoming</Text>
                </TouchableOpacity>
            </View>

            <View style={styles.searchShell}>
                <Ionicons name="search" size={18} color={colors.textTertiary} />
                <TextInput
                    style={styles.searchInput}
                    placeholder="Search by parking name, address, or ref..."
                    placeholderTextColor={colors.textTertiary}
                    value={searchQuery}
                    onChangeText={setSearchQuery}
                />
                {searchQuery ? (
                    <TouchableOpacity onPress={() => setSearchQuery('')}>
                        <Ionicons name="close-circle" size={18} color={colors.textTertiary} />
                    </TouchableOpacity>
                ) : null}
            </View>

            <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                contentContainerStyle={styles.filterRow}
            >
                {FILTERS.map((filter, idx) => (
                    <FilterChip
                        key={filter.label}
                        label={filter.label}
                        active={activeFilter === idx}
                        onPress={() => setActiveFilter(idx)}
                    />
                ))}
            </ScrollView>

            <View style={styles.resultBanner}>
                <View>
                    <Text style={styles.resultLabel}>Showing</Text>
                    <Text style={styles.resultTitle}>{filteredBookings.length} bookings</Text>
                </View>
                <View style={styles.resultPill}>
                    <Text style={styles.resultPillText}>
                        {searchQuery ? 'Search applied' : FILTERS[activeFilter].label}
                    </Text>
                </View>
            </View>
        </View>
    );

    if (myBookingsLoading && !myBookings.length) {
        return (
            <ScreenLayout>
                {headerContent}
                <View style={{ paddingHorizontal: spacing.screenHorizontal }}>
                    <BookingCardSkeleton />
                </View>
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout>
            <View style={styles.screenBody} {...swipeResponder.panHandlers}>
                <FlatList
                    data={filteredBookings}
                    keyExtractor={(item) => item.id}
                    renderItem={({ item }) => (
                        <BookingCard
                            item={item}
                            onPress={() => navigation.navigate('BookingDetail', { bookingId: item.id })}
                        />
                    )}
                    ListHeaderComponent={headerContent}
                    contentContainerStyle={styles.listContent}
                    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                    showsVerticalScrollIndicator={false}
                    ListEmptyComponent={(
                        <EmptyState
                            icon="calendar-outline"
                            title="No bookings found"
                            message={searchQuery ? 'Try a different search or switch the booking filter.' : 'You do not have any bookings yet.'}
                        />
                    )}
                />
            </View>
        </ScreenLayout>
    );
};

const cardStyles = StyleSheet.create({
    card: {
        padding: 0,
        overflow: 'hidden',
        borderRadius: 24,
        marginBottom: spacing.base,
    },
    heroBand: {
        backgroundColor: colors.dark,
        padding: spacing.base,
        position: 'relative',
    },
    heroGlow: {
        position: 'absolute',
        top: -30,
        right: -20,
        width: 140,
        height: 140,
        borderRadius: 70,
        backgroundColor: 'rgba(59,130,246,0.18)',
    },
    heroTopRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: spacing.sm,
    },
    referencePill: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingHorizontal: 10,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
        backgroundColor: 'rgba(255,255,255,0.12)',
    },
    referencePillText: {
        ...typography.caption,
        color: colors.white,
        fontWeight: '700',
    },
    statusPill: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        borderWidth: 1,
        paddingHorizontal: 10,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
    },
    statusPillText: {
        ...typography.caption,
        fontWeight: '700',
    },
    bookingTitle: {
        ...typography.h4,
        color: colors.white,
        marginTop: spacing.base,
    },
    locationRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        marginTop: 6,
    },
    locationText: {
        ...typography.caption,
        color: 'rgba(255,255,255,0.82)',
        flex: 1,
    },
    infoGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: spacing.sm,
        padding: spacing.base,
    },
    infoTile: {
        width: '48%',
        backgroundColor: colors.background,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: colors.borderLight,
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.md,
    },
    infoLabel: {
        ...typography.caption,
        color: colors.textTertiary,
        marginBottom: 4,
    },
    infoValue: {
        ...typography.bodySmall,
        color: colors.textPrimary,
        fontWeight: '600',
    },
    amountValue: {
        ...typography.label,
        color: colors.primary,
        fontWeight: '700',
    },
    footer: {
        borderTopWidth: 1,
        borderTopColor: colors.borderLight,
        paddingHorizontal: spacing.base,
        paddingVertical: spacing.md,
        flexDirection: 'row',
        justifyContent: 'flex-end',
        alignItems: 'center',
    },
});

const styles = StyleSheet.create({
    screenBody: {
        flex: 1,
    },
    heroSection: {
        paddingTop: spacing.sm,
        paddingHorizontal: spacing.screenHorizontal,
        paddingBottom: spacing.sm,
    },
    topTabRow: {
        flexDirection: 'row',
        padding: 4,
        borderRadius: 16,
        backgroundColor: colors.borderLight,
        marginBottom: spacing.sm,
    },
    topTab: {
        flex: 1,
        alignItems: 'center',
        justifyContent: 'center',
        paddingHorizontal: spacing.base,
        paddingVertical: 10,
        borderRadius: 12,
    },
    topTabActive: {
        backgroundColor: colors.surface,
        ...shadows.sm,
    },
    topTabText: {
        ...typography.caption,
        color: colors.textSecondary,
        fontWeight: '700',
    },
    topTabTextActive: {
        color: colors.textPrimary,
    },
    searchShell: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.surface,
        paddingHorizontal: spacing.base,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: colors.borderLight,
        minHeight: 48,
        ...shadows.sm,
    },
    searchInput: {
        flex: 1,
        marginLeft: spacing.sm,
        ...typography.body,
        color: colors.textPrimary,
    },
    filterRow: {
        paddingTop: spacing.base,
        paddingBottom: spacing.xs,
        gap: spacing.sm,
    },
    filterChip: {
        paddingHorizontal: spacing.base,
        paddingVertical: spacing.sm,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.border,
    },
    filterChipActive: {
        backgroundColor: colors.dark,
        borderColor: colors.dark,
    },
    filterChipText: {
        ...typography.caption,
        color: colors.textSecondary,
        fontWeight: '600',
    },
    filterChipTextActive: {
        color: colors.white,
    },
    resultBanner: {
        marginTop: spacing.sm,
        paddingHorizontal: spacing.base,
        paddingVertical: spacing.md,
        borderRadius: 16,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.borderLight,
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        ...shadows.sm,
    },
    resultLabel: {
        ...typography.caption,
        color: colors.textTertiary,
    },
    resultTitle: {
        ...typography.caption,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    resultPill: {
        paddingHorizontal: spacing.sm,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.primarySoft,
    },
    resultPillText: {
        ...typography.caption,
        color: colors.primaryDark,
        fontWeight: '700',
    },
    listContent: {
        paddingBottom: spacing['2xl'],
        flexGrow: 1,
        paddingHorizontal: spacing.screenHorizontal,
    },
});

export default MyBookingsScreen;
