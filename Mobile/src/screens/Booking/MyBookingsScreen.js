/**
 * MyBookingsScreen
 * User's bookings list with status filter tabs
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl, TextInput } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getMyBookingsThunk } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import EmptyState from '../../components/Common/EmptyState';
import { BookingCardSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { BookingStatus } from '../../utils/constants';

const FILTERS = [
    { label: 'All', value: null },
    { label: 'Active', value: [BookingStatus.Confirmed, BookingStatus.InProgress] },
    { label: 'Completed', value: [BookingStatus.Completed] },
    { label: 'Cancelled', value: [BookingStatus.Cancelled, BookingStatus.Rejected] },
];

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

    const filteredBookings = myBookings.filter((b) => {
        const filter = FILTERS[activeFilter].value;
        const matchesStatus = !filter || filter.includes(b.status);
        const matchesSearch = !searchQuery || 
            b.parkingSpaceTitle?.toLowerCase().includes(searchQuery.toLowerCase()) || 
            b.parkingSpaceAddress?.toLowerCase().includes(searchQuery.toLowerCase());
        return matchesStatus && matchesSearch;
    });

    const renderBookingItem = ({ item }) => (
        <Card onPress={() => navigation.navigate('BookingDetail', { bookingId: item.id })} style={styles.bookingCard}>
            <View style={styles.cardHeader}>
                <Text style={styles.bookingTitle} numberOfLines={1}>{item.parkingSpaceTitle}</Text>
                <Badge status={item.status} />
            </View>
            <View style={styles.cardBody}>
                <View style={styles.infoRow}>
                    <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.infoText} numberOfLines={1}>{item.parkingSpaceAddress || 'N/A'}</Text>
                </View>
                <View style={styles.infoRow}>
                    <Ionicons name="calendar-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.infoText}>{formatDate(item.startDateTime)}</Text>
                </View>
                <View style={styles.infoRow}>
                    <Ionicons name="time-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.infoText}>{formatTime(item.startDateTime)} - {formatTime(item.endDateTime)}</Text>
                </View>
            </View>
            <View style={styles.cardFooter}>
                <Text style={styles.refCode}>Ref: {item.bookingReference}</Text>
                <Text style={styles.amount}>{formatCurrency(item.totalAmount)}</Text>
            </View>
        </Card>
    );

    if (myBookingsLoading && !myBookings.length) {
        return (
            <ScreenLayout>
                <View style={styles.header}>
                    <Text style={styles.screenTitle}>My Bookings</Text>
                    <View style={styles.incomingBtn}>
                        <Ionicons name="arrow-down-circle-outline" size={18} color={colors.primary} />
                        <Text style={styles.incomingBtnText}>Incoming Requests</Text>
                    </View>
                </View>
                <View style={styles.searchContainer}>
                    <Ionicons name="search" size={18} color={colors.textTertiary} />
                    <TextInput editable={false} style={styles.searchInput} placeholder="Search bookings..." placeholderTextColor={colors.textTertiary} />
                </View>
                <View style={styles.filterRow}>
                    {FILTERS.map((filter, idx) => (
                        <View key={idx} style={[styles.filterTab, activeFilter === idx && styles.filterTabActive]}>
                            <Text style={[styles.filterTabText, activeFilter === idx && styles.filterTabTextActive]}>{filter.label}</Text>
                        </View>
                    ))}
                </View>
                <BookingCardSkeleton />
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout>
            {/* Header */}
            <View style={styles.header}>
                <Text style={styles.screenTitle}>My Bookings</Text>
                <TouchableOpacity
                    style={styles.incomingBtn}
                    onPress={() => navigation.navigate('IncomingBookings')}
                >
                    <Ionicons name="arrow-down-circle-outline" size={18} color={colors.primary} />
                    <Text style={styles.incomingBtnText}>Incoming Requests</Text>
                </TouchableOpacity>
            </View>

            {/* Search Bar */}
            <View style={styles.searchContainer}>
                <Ionicons name="search" size={18} color={colors.textTertiary} />
                <TextInput
                    style={styles.searchInput}
                    placeholder="Search bookings..."
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

            {/* Filter Tabs */}
            <View style={styles.filterRow}>
                {FILTERS.map((filter, idx) => (
                    <TouchableOpacity
                        key={idx}
                        onPress={() => setActiveFilter(idx)}
                        style={[styles.filterTab, activeFilter === idx && styles.filterTabActive]}
                    >
                        <Text style={[styles.filterTabText, activeFilter === idx && styles.filterTabTextActive]}>
                            {filter.label}
                        </Text>
                    </TouchableOpacity>
                ))}
            </View>

            {/* List */}
            <FlatList
                data={filteredBookings}
                keyExtractor={(item) => item.id}
                renderItem={renderBookingItem}
                contentContainerStyle={styles.listContent}
                refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                showsVerticalScrollIndicator={false}
                ListEmptyComponent={<EmptyState icon="calendar-outline" title="No bookings" message="You don't have any bookings yet" />}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { paddingTop: spacing.md, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.md, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    screenTitle: { ...typography.h2, color: colors.textPrimary },
    incomingBtn: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingVertical: spacing.xs, paddingHorizontal: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.primarySoft },
    incomingBtnText: { ...typography.caption, color: colors.primary, fontWeight: '600' },
    searchContainer: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.background, marginHorizontal: spacing.screenHorizontal, marginBottom: spacing.md, paddingHorizontal: spacing.base, borderRadius: spacing.radius.md, borderWidth: 1, borderColor: colors.borderLight, height: 44 },
    searchInput: { flex: 1, marginLeft: spacing.sm, ...typography.body, color: colors.textPrimary },
    filterRow: { flexDirection: 'row', paddingHorizontal: spacing.screenHorizontal, gap: spacing.sm, marginBottom: spacing.md },
    filterTab: { paddingHorizontal: spacing.base, paddingVertical: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
    filterTabActive: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
    filterTabText: { ...typography.caption, color: colors.textSecondary, fontWeight: '500' },
    filterTabTextActive: { color: colors.primary, fontWeight: '600' },
    listContent: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['2xl'] },
    bookingCard: {},
    cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.sm },
    bookingTitle: { ...typography.label, color: colors.textPrimary, flex: 1, marginRight: spacing.sm },
    cardBody: { gap: spacing.xs },
    infoRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
    infoText: { ...typography.caption, color: colors.textSecondary },
    cardFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.md, paddingTop: spacing.sm, borderTopWidth: 1, borderTopColor: colors.borderLight },
    refCode: { ...typography.caption, color: colors.textTertiary },
    amount: { ...typography.h4, color: colors.primary },
});

export default MyBookingsScreen;
