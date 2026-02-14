/**
 * MyListingsScreen
 * Vendor's parking space listings with add/toggle
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, Switch, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getMyListingsThunk, toggleParkingActiveThunk } from '../../store/slices/parkingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import StarRating from '../../components/Common/StarRating';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency } from '../../utils/formatters';
import { ParkingTypeLabels } from '../../utils/constants';

const ListingCard = ({ listing, onToggle, onEdit }) => (
    <Card>
        <View style={cardStyles.header}>
            <View style={{ flex: 1 }}>
                <Text style={cardStyles.title} numberOfLines={1}>{listing.title}</Text>
                <View style={cardStyles.locationRow}>
                    <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                    <Text style={cardStyles.address} numberOfLines={1}>{listing.address}, {listing.city}</Text>
                </View>
            </View>
            <Switch
                value={listing.isActive}
                onValueChange={() => onToggle(listing.id)}
                trackColor={{ false: colors.lightGray, true: colors.successLight }}
                thumbColor={listing.isActive ? colors.success : colors.mediumGray}
            />
        </View>

        <View style={cardStyles.infoRow}>
            <View style={cardStyles.infoItem}>
                <Text style={cardStyles.infoLabel}>Type</Text>
                <Text style={cardStyles.infoValue}>{ParkingTypeLabels[listing.parkingType]}</Text>
            </View>
            <View style={cardStyles.infoItem}>
                <Text style={cardStyles.infoLabel}>Rate</Text>
                <Text style={cardStyles.infoValue}>{formatCurrency(listing.hourlyRate)}/hr</Text>
            </View>
            <View style={cardStyles.infoItem}>
                <Text style={cardStyles.infoLabel}>Spots</Text>
                <Text style={cardStyles.infoValue}>{listing.availableSpots}/{listing.totalSpots}</Text>
            </View>
        </View>

        <View style={cardStyles.footer}>
            <View style={cardStyles.ratingRow}>
                <StarRating rating={listing.averageRating} size={14} />
                <Text style={cardStyles.ratingText}>{listing.averageRating?.toFixed(1)} ({listing.totalReviews})</Text>
            </View>
            <View style={[cardStyles.statusDot, { backgroundColor: listing.isActive ? colors.success : colors.mediumGray }]} />
            <Text style={[cardStyles.statusText, { color: listing.isActive ? colors.success : colors.mediumGray }]}>
                {listing.isActive ? 'Active' : 'Inactive'}
            </Text>
        </View>
    </Card>
);

const cardStyles = StyleSheet.create({
    header: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between' },
    title: { ...typography.h4, color: colors.textPrimary },
    locationRow: { flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 4 },
    address: { ...typography.caption, color: colors.textTertiary, flex: 1 },
    infoRow: { flexDirection: 'row', justifyContent: 'space-around', marginTop: spacing.md, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.borderLight },
    infoItem: { alignItems: 'center' },
    infoLabel: { ...typography.caption, color: colors.textTertiary },
    infoValue: { ...typography.label, color: colors.textPrimary, marginTop: 2 },
    footer: { flexDirection: 'row', alignItems: 'center', marginTop: spacing.md },
    ratingRow: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 4 },
    ratingText: { ...typography.caption, color: colors.textSecondary },
    statusDot: { width: 8, height: 8, borderRadius: 4, marginRight: 4 },
    statusText: { ...typography.caption, fontWeight: '600' },
});

const MyListingsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { myListings, listingsLoading } = useSelector((s) => s.parking);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getMyListingsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getMyListingsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleToggle = useCallback((id) => {
        dispatch(toggleParkingActiveThunk(id));
    }, [dispatch]);

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <Text style={styles.screenTitle}>My Listings</Text>
                <TouchableOpacity
                    style={styles.addBtn}
                    onPress={() => navigation.navigate('CreateParking')}
                >
                    <Ionicons name="add" size={24} color={colors.white} />
                </TouchableOpacity>
            </View>

            {listingsLoading && !refreshing ? (
                <LoadingScreen />
            ) : (
                <FlatList
                    data={myListings}
                    keyExtractor={(item) => item.id}
                    renderItem={({ item }) => (
                        <ListingCard listing={item} onToggle={handleToggle} onEdit={() => { }} />
                    )}
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
            )}
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: 60,
        paddingHorizontal: spacing.screenHorizontal,
        paddingBottom: spacing.md,
    },
    screenTitle: { ...typography.h2, color: colors.textPrimary },
    addBtn: {
        width: 44,
        height: 44,
        borderRadius: 22,
        backgroundColor: colors.primary,
        justifyContent: 'center',
        alignItems: 'center',
        ...shadows.button,
    },
    listContent: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingBottom: spacing['2xl'],
    },
});

export default MyListingsScreen;
