/**
 * SearchScreen
 * Displays all available parkings by default, with search, filter, and sort
 */

import React, { useState, useCallback, useEffect, useMemo } from 'react';
import { View, Text, TextInput, FlatList, TouchableOpacity, StyleSheet, ScrollView, Modal } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { searchParkingThunk, clearSearch } from '../../store/slices/parkingSlice';
import { getFavoritesThunk } from '../../store/slices/favoriteSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import StarRating from '../../components/Common/StarRating';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency } from '../../utils/formatters';
import { VehicleTypeLabels, ParkingType, ParkingTypeLabels } from '../../utils/constants';

/* ───── Sort Options ───── */
const SORT_OPTIONS = [
    { key: 'default', label: 'Default', icon: 'swap-vertical' },
    { key: 'rating_desc', label: 'Rating: High → Low', icon: 'star' },
    { key: 'price_asc', label: 'Price: Low → High', icon: 'arrow-up' },
    { key: 'price_desc', label: 'Price: High → Low', icon: 'arrow-down' },
    { key: 'spots_desc', label: 'Most Spots', icon: 'car' },
];

/* ───── Parking Card ───── */
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

/* ───── Filter Chip ───── */
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

/* ───── Sort by function ───── */
const sortResults = (results, sortKey) => {
    if (!sortKey || sortKey === 'default') return results;
    const sorted = [...results];
    switch (sortKey) {
        case 'rating_desc':
            return sorted.sort((a, b) => (b.averageRating || 0) - (a.averageRating || 0));
        case 'price_asc':
            return sorted.sort((a, b) => (a.hourlyRate || 0) - (b.hourlyRate || 0));
        case 'price_desc':
            return sorted.sort((a, b) => (b.hourlyRate || 0) - (a.hourlyRate || 0));
        case 'spots_desc':
            return sorted.sort((a, b) => (b.availableSpots || 0) - (a.availableSpots || 0));
        default:
            return sorted;
    }
};

/* ───── Main Screen ───── */
const SearchScreen = ({ navigation, route }) => {
    const dispatch = useDispatch();
    const { searchResults, searchLoading, searchTotalCount } = useSelector((s) => s.parking);
    const { favorites } = useSelector((s) => s.favorite);
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedVehicle, setSelectedVehicle] = useState(null);
    const [selectedParkingType, setSelectedParkingType] = useState(null);
    const [sortBy, setSortBy] = useState('default');
    const [favoritesOnly, setFavoritesOnly] = useState(false);
    const [showSortModal, setShowSortModal] = useState(false);
    const [hasSearched, setHasSearched] = useState(false);

    useEffect(() => {
        dispatch(getFavoritesThunk());
        // Initial load only if we don't have a specific param to handle
        if (route.params?.initialParkingType == null) {
            loadParkings();
        }
    }, []); // Run on mount

    useEffect(() => {
        // Handle incoming params from navigation (e.g. from Garages tile)
        // This runs both on initial mount (if param exists) and when navigating back to tab
        if (route.params?.initialParkingType != null) {
            setSelectedParkingType(route.params.initialParkingType);
            loadParkings(searchQuery.trim() || undefined, selectedVehicle, route.params.initialParkingType);
            // Clear the param so user can manually clear the filter later without it resetting
            navigation.setParams({ initialParkingType: undefined });
        }
    }, [route.params?.initialParkingType, loadParkings, navigation, searchQuery, selectedVehicle]);

    const loadParkings = useCallback((city, vehicleType, parkingType) => {
        setHasSearched(true);
        dispatch(searchParkingThunk({
            city: city || undefined,
            vehicleType: vehicleType ?? undefined,
            parkingType: parkingType ?? undefined,
            page: 1,
            pageSize: 50,
        }));
    }, [dispatch]);

    const handleSearch = useCallback(() => {
        loadParkings(searchQuery.trim() || undefined, selectedVehicle, selectedParkingType);
    }, [loadParkings, searchQuery, selectedVehicle, selectedParkingType]);

    const handleClearSearch = useCallback(() => {
        setSearchQuery('');
        setSelectedVehicle(null);
        setSelectedParkingType(null);
        setFavoritesOnly(false);
        setSortBy('default');
        loadParkings();
    }, [loadParkings]);

    const toggleVehicleFilter = useCallback((value) => {
        const newValue = selectedVehicle === value ? null : value;
        setSelectedVehicle(newValue);
        loadParkings(searchQuery.trim() || undefined, newValue, selectedParkingType);
    }, [selectedVehicle, searchQuery, selectedParkingType, loadParkings]);

    const toggleParkingTypeFilter = useCallback((value) => {
        const newValue = selectedParkingType === value ? null : value;
        setSelectedParkingType(newValue);
        loadParkings(searchQuery.trim() || undefined, selectedVehicle, newValue);
    }, [selectedParkingType, searchQuery, selectedVehicle, loadParkings]);

    // Build set of favorite IDs for quick lookup
    const favoriteIds = useMemo(() => {
        const ids = new Set();
        (favorites || []).forEach((f) => ids.add(f.parkingSpaceId || f.id));
        return ids;
    }, [favorites]);

    // Apply client-side filtering and sorting
    const displayResults = useMemo(() => {
        let results = searchResults;
        if (favoritesOnly) {
            results = results.filter((p) => favoriteIds.has(p.id));
        }
        return sortResults(results, sortBy);
    }, [searchResults, favoritesOnly, favoriteIds, sortBy]);

    const vehicleTypes = Object.entries(VehicleTypeLabels);
    const parkingTypes = Object.entries(ParkingTypeLabels);
    const activeSort = SORT_OPTIONS.find((o) => o.key === sortBy);
    const hasActiveFilters = selectedVehicle != null || selectedParkingType != null || favoritesOnly || sortBy !== 'default';

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

                {/* Sort & Favorites Toggle Row */}
                <View style={styles.sortRow}>
                    <TouchableOpacity style={styles.sortBtn} onPress={() => setShowSortModal(true)}>
                        <Ionicons name="swap-vertical" size={16} color={colors.primary} />
                        <Text style={styles.sortBtnText}>{activeSort?.label || 'Sort'}</Text>
                        <Ionicons name="chevron-down" size={14} color={colors.primary} />
                    </TouchableOpacity>
                    <TouchableOpacity
                        style={[styles.favToggle, favoritesOnly && styles.favToggleActive]}
                        onPress={() => setFavoritesOnly(!favoritesOnly)}
                    >
                        <Ionicons name={favoritesOnly ? 'heart' : 'heart-outline'} size={16} color={favoritesOnly ? colors.white : colors.danger} />
                        <Text style={[styles.favToggleText, favoritesOnly && styles.favToggleTextActive]}>Favorites</Text>
                    </TouchableOpacity>
                    {hasActiveFilters && (
                        <TouchableOpacity onPress={handleClearSearch}>
                            <Text style={styles.clearText}>Clear All</Text>
                        </TouchableOpacity>
                    )}
                </View>

                {/* Vehicle Type Filters */}
                <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterScroll}>
                    {vehicleTypes.map(([value, label]) => (
                        <FilterChip
                            key={`v-${value}`}
                            label={label}
                            active={selectedVehicle === Number(value)}
                            onPress={() => toggleVehicleFilter(Number(value))}
                        />
                    ))}
                </ScrollView>

                {/* Parking Type Filters */}
                <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterScroll}>
                    {parkingTypes.map(([value, label]) => (
                        <FilterChip
                            key={`p-${value}`}
                            label={label}
                            active={selectedParkingType === Number(value)}
                            onPress={() => toggleParkingTypeFilter(Number(value))}
                        />
                    ))}
                </ScrollView>
            </View>

            {/* Results */}
            {searchLoading ? (
                <LoadingScreen message="Loading parking spaces..." />
            ) : displayResults.length > 0 ? (
                <FlatList
                    data={displayResults}
                    keyExtractor={(item) => item.id}
                    renderItem={({ item }) => (
                        <ParkingCard parking={item} onPress={() => navigation.navigate('ParkingDetail', { parkingId: item.id })} />
                    )}
                    ListHeaderComponent={
                        <Text style={styles.resultCount}>
                            {displayResults.length} parking space{displayResults.length !== 1 ? 's' : ''} available
                            {favoritesOnly ? ' (favorites only)' : ''}
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

            {/* Sort Modal */}
            <Modal visible={showSortModal} transparent animationType="fade" onRequestClose={() => setShowSortModal(false)}>
                <TouchableOpacity style={styles.modalOverlay} activeOpacity={1} onPress={() => setShowSortModal(false)}>
                    <View style={styles.modalContent}>
                        <Text style={styles.modalTitle}>Sort By</Text>
                        {SORT_OPTIONS.map((option) => (
                            <TouchableOpacity
                                key={option.key}
                                style={[styles.sortOption, sortBy === option.key && styles.sortOptionActive]}
                                onPress={() => {
                                    setSortBy(option.key);
                                    setShowSortModal(false);
                                }}
                            >
                                <Ionicons
                                    name={option.icon}
                                    size={18}
                                    color={sortBy === option.key ? colors.primary : colors.textSecondary}
                                />
                                <Text style={[styles.sortOptionText, sortBy === option.key && styles.sortOptionTextActive]}>
                                    {option.label}
                                </Text>
                                {sortBy === option.key && (
                                    <Ionicons name="checkmark" size={18} color={colors.primary} />
                                )}
                            </TouchableOpacity>
                        ))}
                    </View>
                </TouchableOpacity>
            </Modal>
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
    sortRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.sm,
        marginTop: spacing.md,
    },
    sortBtn: {
        flexDirection: 'row', alignItems: 'center', gap: 4,
        paddingHorizontal: spacing.md, paddingVertical: spacing.sm,
        borderRadius: spacing.radius.full, backgroundColor: colors.primarySoft,
    },
    sortBtnText: { ...typography.caption, color: colors.primary, fontWeight: '600' },
    favToggle: {
        flexDirection: 'row', alignItems: 'center', gap: 4,
        paddingHorizontal: spacing.md, paddingVertical: spacing.sm,
        borderRadius: spacing.radius.full, borderWidth: 1, borderColor: colors.danger,
    },
    favToggleActive: { backgroundColor: colors.danger, borderColor: colors.danger },
    favToggleText: { ...typography.caption, color: colors.danger, fontWeight: '600' },
    favToggleTextActive: { color: colors.white },
    clearText: { ...typography.caption, color: colors.textTertiary, fontWeight: '500', marginLeft: 'auto' },
    filterScroll: { marginTop: spacing.sm },
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
    // Sort Modal
    modalOverlay: {
        flex: 1, backgroundColor: 'rgba(0,0,0,0.4)',
        justifyContent: 'flex-end',
    },
    modalContent: {
        backgroundColor: colors.surface, borderTopLeftRadius: spacing.radius.xl, borderTopRightRadius: spacing.radius.xl,
        padding: spacing.xl, paddingBottom: 40,
    },
    modalTitle: { ...typography.h3, color: colors.textPrimary, marginBottom: spacing.lg },
    sortOption: {
        flexDirection: 'row', alignItems: 'center', gap: spacing.md,
        paddingVertical: spacing.md, borderBottomWidth: 1, borderBottomColor: colors.borderLight,
    },
    sortOptionActive: { backgroundColor: colors.primarySoft, marginHorizontal: -spacing.md, paddingHorizontal: spacing.md, borderRadius: spacing.radius.md },
    sortOptionText: { ...typography.body, color: colors.textSecondary, flex: 1 },
    sortOptionTextActive: { color: colors.primary, fontWeight: '600' },
});

export default SearchScreen;
