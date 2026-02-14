/**
 * SearchScreen
 * Displays all available parkings by default, with optional search & filter
 */

import React, { useState, useCallback, useEffect } from 'react';
import { View, Text, TextInput, FlatList, TouchableOpacity, StyleSheet, ScrollView } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { searchParkingThunk, clearSearch } from '../../store/slices/parkingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import StarRating from '../../components/Common/StarRating';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency } from '../../utils/formatters';
import { VehicleTypeLabels } from '../../utils/constants';

const ParkingCard = ({ parking, onPress }) => (
    <Card onPress={onPress} style={cardStyles.card}>
        {/* Image placeholder */}
        <View style={cardStyles.imageContainer}>
            <View style={cardStyles.imagePlaceholder}>
                <Ionicons name="car" size={40} color={colors.lightGray} />
            </View>
            <View style={cardStyles.priceTag}>
                <Text style={cardStyles.priceText}>{formatCurrency(parking.hourlyRate)}/hr</Text>
            </View>
            {parking.is24Hours && (
                <View style={cardStyles.badge24h}>
                    <Text style={cardStyles.badge24hText}>24h</Text>
                </View>
            )}
        </View>

        <View style={cardStyles.info}>
            <Text style={cardStyles.title} numberOfLines={1}>{parking.title}</Text>
            <View style={cardStyles.locationRow}>
                <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                <Text style={cardStyles.address} numberOfLines={1}>{parking.address}, {parking.city}</Text>
            </View>
            <View style={cardStyles.metaRow}>
                <View style={cardStyles.ratingRow}>
                    <StarRating rating={parking.averageRating} size={14} />
                    <Text style={cardStyles.ratingText}>{parking.averageRating?.toFixed(1) || '0.0'}</Text>
                    <Text style={cardStyles.reviewCount}>({parking.totalReviews})</Text>
                </View>
                <View style={cardStyles.spotsRow}>
                    <Ionicons name="car-outline" size={14} color={parking.availableSpots > 0 ? colors.success : colors.danger} />
                    <Text style={[cardStyles.spotsText, { color: parking.availableSpots > 0 ? colors.success : colors.danger }]}>
                        {parking.availableSpots} spots
                    </Text>
                </View>
            </View>
        </View>
    </Card>
);

const cardStyles = StyleSheet.create({
    card: { marginHorizontal: spacing.screenHorizontal, overflow: 'hidden', padding: 0 },
    imageContainer: { height: 140, backgroundColor: colors.borderLight, borderTopLeftRadius: spacing.cardRadius, borderTopRightRadius: spacing.cardRadius, position: 'relative' },
    imagePlaceholder: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    priceTag: { position: 'absolute', bottom: 8, right: 8, backgroundColor: colors.primary, paddingHorizontal: 10, paddingVertical: 4, borderRadius: spacing.radius.full },
    priceText: { ...typography.caption, color: colors.white, fontWeight: '700' },
    badge24h: { position: 'absolute', top: 8, left: 8, backgroundColor: colors.accent, paddingHorizontal: 8, paddingVertical: 2, borderRadius: spacing.radius.full },
    badge24hText: { ...typography.caption, color: colors.white, fontWeight: '700', fontSize: 10 },
    info: { padding: spacing.cardPadding },
    title: { ...typography.h4, color: colors.textPrimary },
    locationRow: { flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 4 },
    address: { ...typography.caption, color: colors.textTertiary, flex: 1 },
    metaRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.sm },
    ratingRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
    ratingText: { ...typography.caption, fontWeight: '600', color: colors.textPrimary },
    reviewCount: { ...typography.caption, color: colors.textTertiary },
    spotsRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
    spotsText: { ...typography.caption, fontWeight: '600' },
});

const SearchScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { searchResults, searchLoading, searchTotalCount } = useSelector((s) => s.parking);
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedVehicle, setSelectedVehicle] = useState(null);
    const [hasSearched, setHasSearched] = useState(false);

    // Load all available parkings on mount
    useEffect(() => {
        loadParkings();
    }, []);

    const loadParkings = useCallback((city, vehicleType) => {
        setHasSearched(true);
        dispatch(searchParkingThunk({
            city: city || undefined,
            vehicleType: vehicleType ?? undefined,
            page: 1,
            pageSize: 20,
        }));
    }, [dispatch]);

    const handleSearch = useCallback(() => {
        loadParkings(searchQuery.trim() || undefined, selectedVehicle);
    }, [loadParkings, searchQuery, selectedVehicle]);

    const handleClearSearch = useCallback(() => {
        setSearchQuery('');
        setSelectedVehicle(null);
        loadParkings();
    }, [loadParkings]);

    const toggleVehicleFilter = useCallback((value) => {
        const newValue = selectedVehicle === value ? null : value;
        setSelectedVehicle(newValue);
        loadParkings(searchQuery.trim() || undefined, newValue);
    }, [selectedVehicle, searchQuery, loadParkings]);

    const vehicleTypes = Object.entries(VehicleTypeLabels);

    return (
        <ScreenLayout>
            {/* Search Header */}
            <View style={styles.searchHeader}>
                <Text style={styles.screenTitle}>Find Parking</Text>
                <View style={styles.searchBar}>
                    <Ionicons name="search" size={20} color={colors.textTertiary} />
                    <TextInput
                        value={searchQuery}
                        onChangeText={setSearchQuery}
                        placeholder="Search by city or location..."
                        placeholderTextColor={colors.textTertiary}
                        style={styles.searchInput}
                        onSubmitEditing={handleSearch}
                        returnKeyType="search"
                    />
                    {searchQuery ? (
                        <TouchableOpacity onPress={handleClearSearch}>
                            <Ionicons name="close-circle" size={20} color={colors.textTertiary} />
                        </TouchableOpacity>
                    ) : null}
                </View>

                {/* Vehicle Type Filters */}
                <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterScroll}>
                    {vehicleTypes.map(([value, label]) => (
                        <TouchableOpacity
                            key={value}
                            style={[styles.filterChip, selectedVehicle === Number(value) && styles.filterChipActive]}
                            onPress={() => toggleVehicleFilter(Number(value))}
                        >
                            <Text style={[styles.filterChipText, selectedVehicle === Number(value) && styles.filterChipTextActive]}>
                                {label}
                            </Text>
                        </TouchableOpacity>
                    ))}
                </ScrollView>
            </View>

            {/* Results */}
            {searchLoading ? (
                <LoadingScreen message="Loading parking spaces..." />
            ) : searchResults.length > 0 ? (
                <FlatList
                    data={searchResults}
                    keyExtractor={(item) => item.id}
                    renderItem={({ item }) => (
                        <ParkingCard parking={item} onPress={() => navigation.navigate('ParkingDetail', { parkingId: item.id })} />
                    )}
                    ListHeaderComponent={
                        <Text style={styles.resultCount}>
                            {searchTotalCount} parking space{searchTotalCount !== 1 ? 's' : ''} available
                        </Text>
                    }
                    showsVerticalScrollIndicator={false}
                    contentContainerStyle={{ paddingBottom: spacing['2xl'] }}
                />
            ) : hasSearched ? (
                <EmptyState
                    icon="car-outline"
                    title="No parking spaces found"
                    message="Try a different search or clear filters"
                    buttonTitle="Clear Filters"
                    onButtonPress={handleClearSearch}
                />
            ) : (
                <EmptyState icon="search-outline" title="Find Parking" message="Loading available parking spaces..." />
            )}
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    searchHeader: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingTop: 60,
        paddingBottom: spacing.base,
        backgroundColor: colors.surface,
        ...shadows.sm,
    },
    screenTitle: { ...typography.h2, color: colors.textPrimary, marginBottom: spacing.base },
    searchBar: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.background,
        borderRadius: spacing.inputRadius,
        paddingHorizontal: spacing.base,
        gap: spacing.sm,
        borderWidth: 1,
        borderColor: colors.border,
    },
    searchInput: { flex: 1, ...typography.body, color: colors.textPrimary, paddingVertical: spacing.inputPaddingV },
    filterScroll: { marginTop: spacing.md },
    filterChip: {
        paddingHorizontal: spacing.base,
        paddingVertical: spacing.sm,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.background,
        borderWidth: 1,
        borderColor: colors.border,
        marginRight: spacing.sm,
    },
    filterChipActive: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
    filterChipText: { ...typography.caption, color: colors.textSecondary, fontWeight: '500' },
    filterChipTextActive: { color: colors.primary, fontWeight: '600' },
    resultCount: { ...typography.bodySmall, color: colors.textSecondary, paddingHorizontal: spacing.screenHorizontal, paddingVertical: spacing.md },
});

export default SearchScreen;
