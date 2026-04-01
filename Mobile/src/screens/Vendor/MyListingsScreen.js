/**
 * MyListingsScreen
 * Vendor's parking space listings with add/toggle
 */

import React, { useEffect, useCallback, useState } from 'react';
import { EventBus } from '../../utils/EventBus';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl, Alert, Image } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getMyListingsThunk, toggleParkingActiveThunk, deleteParkingThunk } from '../../store/slices/parkingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import EmptyState from '../../components/Common/EmptyState';
import SwipeableRow from '../../components/Common/SwipeableRow';
import { ListItemSkeleton } from '../../components/Common/ShimmerPlaceholder';
import StarRating from '../../components/Common/StarRating';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, getPrimaryParkingImageUrl } from '../../utils/formatters';
import { ParkingTypeLabels } from '../../utils/constants';

const ListingCard = ({ listing, onToggle, onEdit, onPress, isToggling }) => {
    const imageUrl = getPrimaryParkingImageUrl(listing);
    const availableSpots = listing.availableSpots || 0;
    const totalSpots = listing.totalSpots || 0;
    const occupancyPercent = totalSpots > 0
        ? Math.min(100, Math.max(0, Math.round(((totalSpots - availableSpots) / totalSpots) * 100)))
        : 0;

    return (
        <TouchableOpacity activeOpacity={0.86} onPress={onPress}>
            <Card style={cardStyles.card}>
                <View style={cardStyles.mediaHeader}>
                    {imageUrl ? (
                        <Image
                            source={{ uri: imageUrl }}
                            style={cardStyles.coverImage}
                        />
                    ) : (
                        <View style={cardStyles.coverPlaceholder}>
                            <Ionicons name="image-outline" size={28} color={colors.textTertiary} />
                        </View>
                    )}
                    <View style={cardStyles.mediaOverlay} />

                    <View style={cardStyles.topBadgeRow}>
                        <View style={[cardStyles.statusBadge, listing.isActive ? cardStyles.activeBadge : cardStyles.pausedBadge]}>
                            <Text style={[cardStyles.statusBadgeText, listing.isActive ? cardStyles.activeBadgeText : cardStyles.pausedBadgeText]}>
                                {listing.isActive ? 'Active' : 'Paused'}
                            </Text>
                        </View>
                        <View style={cardStyles.rateBadge}>
                            <Text style={cardStyles.rateBadgeText}>{formatCurrency(listing.hourlyRate)}/hr</Text>
                        </View>
                    </View>
                </View>

                <View style={cardStyles.header}>
                    <View style={{ flex: 1 }}>
                        <Text style={cardStyles.title} numberOfLines={1}>{listing.title}</Text>
                        <View style={cardStyles.locationRow}>
                            <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                            <Text style={cardStyles.address} numberOfLines={1}>{listing.address}, {listing.city}</Text>
                        </View>
                    </View>
                    <TouchableOpacity style={cardStyles.iconBtn} onPress={() => onEdit(listing)}>
                        <Ionicons name="create-outline" size={18} color={colors.primary} />
                    </TouchableOpacity>
                </View>

                <View style={cardStyles.statsRow}>
                    <View style={cardStyles.statCard}>
                        <Text style={cardStyles.statLabel}>Type</Text>
                        <Text style={cardStyles.statValue}>{ParkingTypeLabels[listing.parkingType]}</Text>
                    </View>
                    <View style={cardStyles.statCard}>
                        <Text style={cardStyles.statLabel}>Available</Text>
                        <Text style={cardStyles.statValue}>{availableSpots}/{totalSpots}</Text>
                    </View>
                    <View style={cardStyles.statCard}>
                        <Text style={cardStyles.statLabel}>Filled</Text>
                        <Text style={cardStyles.statValue}>{occupancyPercent}%</Text>
                    </View>
                </View>

                <View style={cardStyles.progressTrack}>
                    <View style={[cardStyles.progressFill, { width: `${Math.max(occupancyPercent, totalSpots ? 8 : 0)}%` }]} />
                </View>

                <View style={cardStyles.footer}>
                    <View style={cardStyles.ratingPill}>
                        <StarRating rating={listing.averageRating} size={14} />
                        <Text style={cardStyles.ratingText}>{listing.averageRating?.toFixed(1) || '0.0'}</Text>
                        <Text style={cardStyles.ratingMuted}>({listing.totalReviews || 0})</Text>
                    </View>
                    <Button
                        title={listing.isActive ? 'Pause Listing' : 'Activate Listing'}
                        variant={listing.isActive ? 'outline' : 'primary'}
                        onPress={() => onToggle(listing.id)}
                        loading={isToggling}
                        style={cardStyles.actionButton}
                        textStyle={cardStyles.actionButtonText}
                    />
                </View>
            </Card>
        </TouchableOpacity>
    );
};

const MyListingsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { myListings, listingsLoading } = useSelector((s) => s.parking);
    const [refreshing, setRefreshing] = useState(false);
    const [togglingId, setTogglingId] = useState(null);
    const [deletingId, setDeletingId] = useState(null);

    useEffect(() => {
        dispatch(getMyListingsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getMyListingsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleToggle = useCallback((id) => {
        setTogglingId(id);
        dispatch(toggleParkingActiveThunk(id)).then(async (res) => {
            if (res.error) {
                EventBus.emit('SHOW_ERROR_BANNER', {
                    title: 'Error',
                    message: res.payload || 'Failed to update listing status'
                });
            } else {
                await dispatch(getMyListingsThunk());
            }
            setTogglingId(null);
        });
    }, [dispatch]);

    const handleDelete = useCallback((id) => {
        Alert.alert('Delete Space', 'Are you sure you want to permanently delete this parking space?', [
            { text: 'Cancel', style: 'cancel' },
            {
                text: 'Delete',
                style: 'destructive',
                onPress: () => {
                    setDeletingId(id);
                    dispatch(deleteParkingThunk(id)).then(async (res) => {
                        if (res.error) {
                            EventBus.emit('SHOW_ERROR_BANNER', {
                                title: 'Error',
                                message: res.payload || 'Failed to delete listing'
                            });
                        } else {
                            await dispatch(getMyListingsThunk());
                        }
                        setDeletingId(null);
                    });
                }
            }
        ]);
    }, [dispatch]);

    const renderHeader = (
        <View style={styles.heroSection}>
            <View style={styles.header}>
                <Text style={styles.screenTitle}>My Listings</Text>
                <TouchableOpacity
                    style={styles.addBtn}
                    onPress={() => navigation.navigate('CreateParking')}
                >
                    <Ionicons name="add" size={24} color={colors.white} />
                </TouchableOpacity>
            </View>
        </View>
    );

    if (listingsLoading && !myListings.length) {
        return (
            <ScreenLayout>
                {renderHeader}
                <View style={{ paddingHorizontal: spacing.screenHorizontal, paddingTop: spacing.md }}>
                    {[1, 2, 3].map((i) => (
                        <ListItemSkeleton key={i} />
                    ))}
                </View>
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout>
            <FlatList
                data={myListings}
                keyExtractor={(item) => item.id}
                renderItem={({ item }) => (
                    <SwipeableRow
                        onDelete={() => handleDelete(item.id)}
                        isDeleting={deletingId === item.id}
                    >
                        <ListingCard
                            listing={item}
                            onToggle={handleToggle}
                            isToggling={togglingId === item.id}
                            onEdit={(data) => navigation.navigate('CreateParking', { editData: data })}
                            onPress={() => navigation.navigate('ParkingDetail', { parkingId: item.id })}
                        />
                    </SwipeableRow>
                )}
                ListHeaderComponent={renderHeader}
                contentContainerStyle={styles.listContent}
                refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                showsVerticalScrollIndicator={false}
                ListEmptyComponent={
                    <EmptyState
                        icon="location-outline"
                        title="No listings yet"
                        message="Add your first parking space to start earning"
                        actionLabel="Add Parking Space"
                        onAction={() => navigation.navigate('CreateParking')}
                    />
                }
            />
        </ScreenLayout>
    );
};

const cardStyles = StyleSheet.create({
    card: {
        borderRadius: 24,
        padding: spacing.base,
        marginBottom: spacing.base,
    },
    mediaHeader: {
        marginBottom: spacing.md,
        position: 'relative',
    },
    coverImage: {
        width: '100%',
        height: 168,
        borderRadius: 20,
        backgroundColor: colors.borderLight,
    },
    coverPlaceholder: {
        width: '100%',
        height: 168,
        borderRadius: 20,
        backgroundColor: colors.background,
        justifyContent: 'center',
        alignItems: 'center',
    },
    mediaOverlay: {
        ...StyleSheet.absoluteFillObject,
        borderRadius: 20,
        backgroundColor: 'rgba(15, 23, 42, 0.12)',
    },
    topBadgeRow: {
        position: 'absolute',
        top: 12,
        left: 12,
        right: 12,
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    statusBadge: {
        paddingHorizontal: 10,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
    },
    activeBadge: {
        backgroundColor: colors.successSoft,
    },
    pausedBadge: {
        backgroundColor: colors.warningSoft,
    },
    statusBadgeText: {
        ...typography.caption,
        fontWeight: '700',
    },
    activeBadgeText: {
        color: colors.successDark,
    },
    pausedBadgeText: {
        color: colors.warningDark,
    },
    rateBadge: {
        paddingHorizontal: 12,
        paddingVertical: 7,
        borderRadius: spacing.radius.full,
        backgroundColor: 'rgba(15, 23, 42, 0.9)',
    },
    rateBadgeText: {
        ...typography.caption,
        color: colors.white,
        fontWeight: '700',
    },
    header: {
        flexDirection: 'row',
        alignItems: 'flex-start',
        justifyContent: 'space-between',
        gap: spacing.base,
    },
    title: {
        ...typography.h4,
        color: colors.textPrimary,
    },
    locationRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        marginTop: 4,
    },
    address: {
        ...typography.caption,
        color: colors.textTertiary,
        flex: 1,
    },
    iconBtn: {
        width: 36,
        height: 36,
        borderRadius: 18,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
    },
    statsRow: {
        flexDirection: 'row',
        gap: spacing.sm,
        marginTop: spacing.md,
    },
    statCard: {
        flex: 1,
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.md,
        borderRadius: 16,
        backgroundColor: colors.background,
        borderWidth: 1,
        borderColor: colors.borderLight,
    },
    statLabel: {
        ...typography.caption,
        color: colors.textTertiary,
        marginBottom: 4,
    },
    statValue: {
        ...typography.label,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    progressTrack: {
        height: 10,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.borderLight,
        marginTop: spacing.base,
        overflow: 'hidden',
    },
    progressFill: {
        height: '100%',
        borderRadius: spacing.radius.full,
        backgroundColor: colors.primary,
    },
    footer: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: spacing.base,
        marginTop: spacing.base,
    },
    ratingPill: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingHorizontal: 10,
        paddingVertical: 7,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.background,
        borderWidth: 1,
        borderColor: colors.border,
    },
    ratingText: {
        ...typography.caption,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    ratingMuted: {
        ...typography.caption,
        color: colors.textTertiary,
    },
    actionButton: {
        paddingVertical: 10,
        paddingHorizontal: 16,
        minHeight: 40,
    },
    actionButtonText: {
        ...typography.caption,
        fontWeight: '700',
    },
});

const styles = StyleSheet.create({
    heroSection: {
        paddingTop: spacing.xs,
        paddingHorizontal: spacing.screenHorizontal,
        paddingBottom: spacing.sm,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: spacing.base,
    },
    screenTitle: {
        ...typography.h3,
        color: colors.textPrimary,
    },
    addBtn: {
        width: 42,
        height: 42,
        borderRadius: 21,
        backgroundColor: colors.primary,
        justifyContent: 'center',
        alignItems: 'center',
        ...shadows.button,
    },
    listContent: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingBottom: spacing['2xl'],
        flexGrow: 1,
    },
});

export default MyListingsScreen;
