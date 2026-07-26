/**
 * MyPassesScreen
 * Active parking passes for the signed-in user
 */

import React, { useCallback, useEffect } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getMyPassesThunk } from '../../store/slices/passSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import EmptyState from '../../components/Common/EmptyState';
import EnhancedRefreshControl, { useEnhancedRefresh } from '../../components/Common/EnhancedRefreshControl';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { formatDate, formatCurrency } from '../../utils/formatters';

const PASS_TYPE_LABELS = {
    0: 'Monthly',
    1: 'Weekly',
    2: 'Corporate',
    Monthly: 'Monthly',
    Weekly: 'Weekly',
    Corporate: 'Corporate',
};

const USAGE_MODE_LABELS = {
    0: 'Unlimited entries',
    1: 'Limited daily hours',
    UnlimitedEntries: 'Unlimited entries',
    LimitedHoursPerDay: 'Limited daily hours',
};

const getPassTypeLabel = (pass) => PASS_TYPE_LABELS[pass.passType ?? pass.type ?? pass.passTypeKind] || 'Parking pass';
const getUsageLabel = (pass) => USAGE_MODE_LABELS[pass.usageMode] || 'Standard access';
const getStartDate = (pass) => pass.startDateUtc || pass.startDate || pass.validFrom || pass.validFromUtc;
const getEndDate = (pass) => pass.endDateUtc || pass.endDate || pass.validUntil || pass.validUntilUtc;
const getLocation = (pass) =>
    pass.parkingSpaceTitle ||
    pass.parkingTitle ||
    pass.parkingSpaceName ||
    pass.parkingZoneCode ||
    pass.zoneCode ||
    'All eligible spaces';

const getStatus = (pass) => {
    const rawStatus = pass.statusLabel || pass.status;
    if (rawStatus != null) {
        return String(rawStatus);
    }

    const endDate = getEndDate(pass);
    if (endDate && new Date(endDate) < new Date()) {
        return 'Expired';
    }

    return pass.isActive === false ? 'Inactive' : 'Active';
};

const getStatusColor = (status) => {
    const normalized = String(status).toLowerCase();
    if (normalized.includes('expired') || normalized.includes('inactive')) return colors.textTertiary;
    if (normalized.includes('active')) return colors.success;
    return colors.primary;
};

const PassCard = ({ pass }) => {
    const status = getStatus(pass);
    const statusColor = getStatusColor(status);
    const price = pass.price ?? pass.amount ?? pass.totalAmount ?? pass.discountedAmount;
    const dailyHourLimit = pass.dailyHourLimit ?? pass.hoursPerDay;

    return (
        <Card style={styles.passCard}>
            <View style={styles.passHeader}>
                <View style={styles.iconWrap}>
                    <Ionicons name="ticket-outline" size={24} color={colors.primary} />
                </View>
                <View style={styles.passTitleWrap}>
                    <Text style={styles.passTitle}>{getPassTypeLabel(pass)}</Text>
                    <Text style={styles.passLocation} numberOfLines={1}>{getLocation(pass)}</Text>
                </View>
                <View style={[styles.statusPill, { backgroundColor: `${statusColor}1A` }]}>
                    <Text style={[styles.statusText, { color: statusColor }]}>{status}</Text>
                </View>
            </View>

            <View style={styles.divider} />

            <View style={styles.detailGrid}>
                <View style={styles.detailItem}>
                    <Text style={styles.detailLabel}>Valid from</Text>
                    <Text style={styles.detailValue}>{formatDate(getStartDate(pass)) || 'Not set'}</Text>
                </View>
                <View style={styles.detailItem}>
                    <Text style={styles.detailLabel}>Valid until</Text>
                    <Text style={styles.detailValue}>{formatDate(getEndDate(pass)) || 'Not set'}</Text>
                </View>
            </View>

            <View style={styles.metaRow}>
                <Ionicons name="repeat-outline" size={16} color={colors.textTertiary} />
                <Text style={styles.metaText}>
                    {dailyHourLimit ? `${dailyHourLimit} hrs/day` : getUsageLabel(pass)}
                </Text>
            </View>

            {price != null && (
                <View style={styles.metaRow}>
                    <Ionicons name="card-outline" size={16} color={colors.textTertiary} />
                    <Text style={styles.metaText}>{formatCurrency(price)}</Text>
                </View>
            )}
        </Card>
    );
};

const MyPassesScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { passes, loading, error } = useSelector((state) => state.pass);

    const loadPasses = useCallback(async () => {
        await dispatch(getMyPassesThunk());
    }, [dispatch]);

    const { refreshing, onRefresh, lastRefreshed } = useEnhancedRefresh(loadPasses);

    useEffect(() => {
        loadPasses();
    }, [loadPasses]);

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <Text style={styles.title}>My Passes</Text>
                <View style={{ width: 24 }} />
            </View>

            <FlatList
                data={passes}
                keyExtractor={(item, index) => String(item.id || item.passId || index)}
                renderItem={({ item }) => <PassCard pass={item} />}
                ListEmptyComponent={
                    <EmptyState
                        icon={error ? 'alert-circle-outline' : 'ticket-outline'}
                        title={error ? 'Could not load passes' : 'No active passes'}
                        message={error ? 'We are experiencing temporary issues. Please try again later.' : 'Your weekly, monthly, and corporate parking passes will appear here.'}
                    />
                }
                refreshControl={
                    <EnhancedRefreshControl refreshing={refreshing} onRefresh={onRefresh} lastRefreshed={lastRefreshed} />
                }
                showsVerticalScrollIndicator={false}
                contentContainerStyle={styles.listContent}
            />

            {loading && passes.length > 0 && (
                <View style={styles.loadingStrip}>
                    <Text style={styles.loadingText}>Refreshing passes...</Text>
                </View>
            )}
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: spacing.md,
        paddingBottom: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
    },
    title: { ...typography.h3, color: colors.textPrimary },
    listContent: {
        paddingBottom: spacing['3xl'],
    },
    passCard: {
        marginHorizontal: spacing.screenHorizontal,
    },
    passHeader: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
    },
    iconWrap: {
        width: 44,
        height: 44,
        borderRadius: 22,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
    },
    passTitleWrap: {
        flex: 1,
        minWidth: 0,
    },
    passTitle: {
        ...typography.label,
        color: colors.textPrimary,
    },
    passLocation: {
        ...typography.caption,
        color: colors.textTertiary,
        marginTop: 2,
    },
    statusPill: {
        borderRadius: spacing.radius.full,
        paddingHorizontal: spacing.sm,
        paddingVertical: 4,
    },
    statusText: {
        ...typography.caption,
        fontWeight: '700',
        textTransform: 'capitalize',
    },
    divider: {
        height: 1,
        backgroundColor: colors.borderLight,
        marginVertical: spacing.md,
    },
    detailGrid: {
        flexDirection: 'row',
        gap: spacing.md,
        marginBottom: spacing.md,
    },
    detailItem: {
        flex: 1,
    },
    detailLabel: {
        ...typography.caption,
        color: colors.textTertiary,
        marginBottom: 2,
    },
    detailValue: {
        ...typography.bodySmall,
        color: colors.textPrimary,
        fontWeight: '600',
    },
    metaRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.xs,
        marginTop: spacing.xs,
    },
    metaText: {
        ...typography.caption,
        color: colors.textSecondary,
    },
    loadingStrip: {
        position: 'absolute',
        left: spacing.screenHorizontal,
        right: spacing.screenHorizontal,
        bottom: spacing.lg,
        paddingVertical: spacing.sm,
        borderRadius: spacing.radius.md,
        backgroundColor: colors.surface,
        alignItems: 'center',
    },
    loadingText: {
        ...typography.caption,
        color: colors.textTertiary,
    },
});

export default MyPassesScreen;
