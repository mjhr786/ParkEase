/**
 * SearchScreen
 * Displays all available parkings by default, with search, filter, and sort
 */

import React, { useState, useCallback, useEffect, useMemo } from 'react';
import { View, Text, TextInput, FlatList, TouchableOpacity, StyleSheet, ScrollView, Modal, Image } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { searchParkingThunk } from '../../store/slices/parkingSlice';
import { getFavoritesThunk } from '../../store/slices/favoriteSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import StarRating from '../../components/Common/StarRating';
import EmptyState from '../../components/Common/EmptyState';
import { SearchSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, getPrimaryParkingImageUrl } from '../../utils/formatters';
import { VehicleTypeLabels, ParkingTypeLabels } from '../../utils/constants';

const SORT_OPTIONS = [
    { key: 'default', label: 'Default', icon: 'swap-vertical' },
    { key: 'rating_desc', label: 'Rating: High to Low', icon: 'star' },
    { key: 'price_asc', label: 'Price: Low to High', icon: 'arrow-up' },
    { key: 'price_desc', label: 'Price: High to Low', icon: 'arrow-down' },
    { key: 'spots_desc', label: 'Most Spots', icon: 'car' },
];

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

const ParkingCard = ({ parking, onPress }) => {
    const imageUrl = getPrimaryParkingImageUrl(parking);
    const rating = parking.averageRating?.toFixed(1) || '0.0';
    const availableSpots = parking.availableSpots || 0;
    const typeLabel = ParkingTypeLabels[parking.parkingType] || 'Parking';

    return (
        <Card onPress={onPress} style={cardStyles.card}>
            <View style={cardStyles.imageContainer}>
                {imageUrl ? (
                    <Image source={{ uri: imageUrl }} style={cardStyles.cardImage} />
                ) : (
                    <View style={cardStyles.imagePlaceholder}>
                        <Ionicons name="image-outline" size={34} color={colors.lightGray} />
                    </View>
                )}
                <View style={cardStyles.imageOverlay} />

                <View style={cardStyles.topRow}>
                    {parking.is24Hours ? (
                        <View style={cardStyles.badge24h}>
                            <Ionicons name="time-outline" size={12} color={colors.white} />
                            <Text style={cardStyles.badge24hText}>24h</Text>
                        </View>
                    ) : (
                        <View />
                    )}

                    <View style={cardStyles.ratingPill}>
                        <Ionicons name="star" size={12} color={colors.accent} />
                        <Text style={cardStyles.ratingPillText}>{rating}</Text>
                    </View>
                </View>

                <View style={cardStyles.bottomRow}>
                    <View style={cardStyles.typePill}>
                        <Text style={cardStyles.typePillText}>{typeLabel}</Text>
                    </View>
                    <View style={cardStyles.priceTag}>
                        <Text style={cardStyles.priceText}>{formatCurrency(parking.hourlyRate)}/hr</Text>
                    </View>
                </View>
            </View>

            <View style={cardStyles.info}>
                <Text style={cardStyles.title} numberOfLines={1}>{parking.title}</Text>
                <View style={cardStyles.locationRow}>
                    <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                    <Text style={cardStyles.address} numberOfLines={1}>
                        {parking.address}, {parking.city}
                    </Text>
                </View>
                <View style={cardStyles.metaRow}>
                    <View style={cardStyles.metaChip}>
                        <StarRating rating={parking.averageRating} size={13} />
                        <Text style={cardStyles.metaChipText}>{rating}</Text>
                        <Text style={cardStyles.metaChipMuted}>({parking.totalReviews})</Text>
                    </View>
                    <View style={cardStyles.metaChip}>
                        <Ionicons
                            name="car-sport-outline"
                            size={14}
                            color={availableSpots > 0 ? colors.successDark : colors.dangerDark}
                        />
                        <Text
                            style={[
                                cardStyles.metaChipText,
                                { color: availableSpots > 0 ? colors.successDark : colors.dangerDark },
                            ]}
                        >
                            {availableSpots} spot{availableSpots === 1 ? '' : 's'}
                        </Text>
                    </View>
                </View>
            </View>
        </Card>
    );
};

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

    useEffect(() => {
        dispatch(getFavoritesThunk());
        if (route.params?.initialParkingType == null) {
            loadParkings();
        }
    }, []);

    useEffect(() => {
        if (route.params?.initialParkingType != null) {
            setSelectedParkingType(route.params.initialParkingType);
            loadParkings(searchQuery.trim() || undefined, selectedVehicle, route.params.initialParkingType);
            navigation.setParams({ initialParkingType: undefined });
        }
    }, [route.params?.initialParkingType, loadParkings, navigation, searchQuery, selectedVehicle]);

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

    const favoriteIds = useMemo(() => {
        const ids = new Set();
        (favorites || []).forEach((favorite) => ids.add(favorite.parkingSpaceId || favorite.id));
        return ids;
    }, [favorites]);

    const displayResults = useMemo(() => {
        let results = searchResults;
        if (favoritesOnly) {
            results = results.filter((parking) => favoriteIds.has(parking.id));
        }
        return sortResults(results, sortBy);
    }, [searchResults, favoritesOnly, favoriteIds, sortBy]);

    const vehicleTypes = Object.entries(VehicleTypeLabels);
    const parkingTypes = Object.entries(ParkingTypeLabels);
    const activeSort = SORT_OPTIONS.find((option) => option.key === sortBy);
    const activeFilterCount = [
        selectedVehicle != null,
        selectedParkingType != null,
        favoritesOnly,
        sortBy !== 'default',
    ].filter(Boolean).length;
    const hasActiveFilters = activeFilterCount > 0;
    const resultLabel = `${displayResults.length} of ${searchTotalCount || displayResults.length}`;

    const headerContent = (
        <>
            <View style={styles.searchBarCard}>
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

                <View style={styles.actionRow}>
                    <TouchableOpacity style={styles.sortBtn} onPress={() => setShowSortModal(true)}>
                        <Ionicons name="swap-vertical" size={16} color={colors.primary} />
                        <Text style={styles.sortBtnText}>{activeSort?.label || 'Sort'}</Text>
                        <Ionicons name="chevron-down" size={14} color={colors.primary} />
                    </TouchableOpacity>

                    <TouchableOpacity
                        style={[styles.favToggle, favoritesOnly && styles.favToggleActive]}
                        onPress={() => setFavoritesOnly(!favoritesOnly)}
                    >
                        <Ionicons
                            name={favoritesOnly ? 'heart' : 'heart-outline'}
                            size={16}
                            color={favoritesOnly ? colors.white : colors.danger}
                        />
                        <Text style={[styles.favToggleText, favoritesOnly && styles.favToggleTextActive]}>
                            Favorites
                        </Text>
                    </TouchableOpacity>

                    {hasActiveFilters && (
                        <TouchableOpacity onPress={handleClearSearch} style={styles.resetPill}>
                            <Text style={styles.resetPillText}>Reset</Text>
                        </TouchableOpacity>
                    )}
                </View>
            </View>

            <View style={styles.filterSection}>
                <View style={styles.filterTitleRow}>
                    <Text style={styles.filterTitle}>Vehicle Type</Text>
                    {selectedVehicle != null && <Text style={styles.filterHint}>1 selected</Text>}
                </View>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    {vehicleTypes.map(([value, label]) => (
                        <FilterChip
                            key={`vehicle-${value}`}
                            label={label}
                            active={selectedVehicle === Number(value)}
                            onPress={() => toggleVehicleFilter(Number(value))}
                        />
                    ))}
                </ScrollView>
            </View>

            <View style={styles.filterSection}>
                <View style={styles.filterTitleRow}>
                    <Text style={styles.filterTitle}>Parking Type</Text>
                    {selectedParkingType != null && <Text style={styles.filterHint}>Focused</Text>}
                </View>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <FilterChip
                        label="All"
                        active={selectedParkingType === null}
                        onPress={() => toggleParkingTypeFilter(null)}
                    />
                    {parkingTypes.map(([value, label]) => (
                        <FilterChip
                            key={`parking-${value}`}
                            label={label}
                            active={selectedParkingType === Number(value)}
                            onPress={() => toggleParkingTypeFilter(Number(value))}
                        />
                    ))}
                </ScrollView>
            </View>

            <View style={styles.resultsBanner}>
                <View>
                    <Text style={styles.resultsLabel}>Results</Text>
                    <Text style={styles.resultsTitle}>{resultLabel} spaces</Text>
                </View>
                <View style={styles.resultsPill}>
                    <Text style={styles.resultsPillText}>
                        {hasActiveFilters ? `${activeFilterCount} filters active` : 'Browse all'}
                    </Text>
                </View>
            </View>
        </>
    );

    if (searchLoading && !searchResults.length) {
        return (
            <ScreenLayout>
                <View style={styles.headerShell}>
                    {headerContent}
                </View>
                <SearchSkeleton />
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout>
            <FlatList
                data={displayResults}
                keyExtractor={(item) => item.id}
                renderItem={({ item }) => (
                    <ParkingCard
                        parking={item}
                        onPress={() => navigation.navigate('ParkingDetail', { parkingId: item.id })}
                    />
                )}
                ListHeaderComponent={<View style={styles.headerShell}>{headerContent}</View>}
                ListEmptyComponent={
                    hasSearched ? (
                        <EmptyState
                            icon="car-outline"
                            title="No parking spaces found"
                            message="Try a different search or clear filters"
                            actionLabel="Clear Filters"
                            onAction={handleClearSearch}
                        />
                    ) : (
                        <EmptyState
                            icon="search-outline"
                            title="Find Parking"
                            message="Loading available parking spaces..."
                        />
                    )
                }
                showsVerticalScrollIndicator={false}
                contentContainerStyle={styles.listContent}
            />

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

const cardStyles = StyleSheet.create({
    card: {
        marginHorizontal: spacing.screenHorizontal,
        marginBottom: spacing.base,
        padding: 0,
        borderRadius: 24,
        overflow: 'hidden',
        ...shadows.card,
    },
    imageContainer: {
        height: 172,
        backgroundColor: colors.borderLight,
        position: 'relative',
    },
    cardImage: {
        width: '100%',
        height: '100%',
    },
    imagePlaceholder: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    imageOverlay: {
        ...StyleSheet.absoluteFillObject,
        backgroundColor: 'rgba(15, 23, 42, 0.16)',
    },
    topRow: {
        position: 'absolute',
        top: 12,
        left: 12,
        right: 12,
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    bottomRow: {
        position: 'absolute',
        bottom: 12,
        left: 12,
        right: 12,
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: spacing.sm,
    },
    badge24h: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        paddingHorizontal: 10,
        paddingVertical: 5,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.accent,
    },
    badge24hText: {
        ...typography.caption,
        color: colors.white,
        fontWeight: '700',
    },
    ratingPill: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        paddingHorizontal: 10,
        paddingVertical: 5,
        borderRadius: spacing.radius.full,
        backgroundColor: 'rgba(255,255,255,0.95)',
    },
    ratingPillText: {
        ...typography.caption,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    typePill: {
        paddingHorizontal: 10,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
        backgroundColor: 'rgba(255,255,255,0.94)',
    },
    typePillText: {
        ...typography.caption,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    priceTag: {
        paddingHorizontal: 12,
        paddingVertical: 7,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.dark,
    },
    priceText: {
        ...typography.caption,
        color: colors.white,
        fontWeight: '700',
    },
    info: {
        padding: spacing.lg,
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
    metaRow: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: spacing.sm,
        marginTop: spacing.md,
    },
    metaChip: {
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
    metaChipText: {
        ...typography.caption,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    metaChipMuted: {
        ...typography.caption,
        color: colors.textTertiary,
    },
});

const styles = StyleSheet.create({
    headerShell: {
        paddingTop: spacing.xs,
        paddingBottom: spacing.base,
    },
    searchBarCard: {
        marginHorizontal: spacing.screenHorizontal,
        marginTop: spacing.xs,
        padding: spacing.base,
        borderRadius: 18,
        backgroundColor: colors.surface,
        ...shadows.card,
    },
    searchBar: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.background,
        borderRadius: 18,
        paddingHorizontal: spacing.base,
        gap: spacing.sm,
        borderWidth: 1,
        borderColor: colors.border,
    },
    searchInput: {
        flex: 1,
        ...typography.body,
        color: colors.textPrimary,
        paddingVertical: spacing.inputPaddingV,
    },
    actionRow: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: spacing.sm,
        marginTop: spacing.md,
    },
    sortBtn: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        paddingHorizontal: spacing.md,
        paddingVertical: 10,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.primarySoft,
    },
    sortBtnText: {
        ...typography.caption,
        color: colors.primary,
        fontWeight: '700',
    },
    favToggle: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        paddingHorizontal: spacing.md,
        paddingVertical: 10,
        borderRadius: spacing.radius.full,
        borderWidth: 1,
        borderColor: colors.danger,
    },
    favToggleActive: {
        backgroundColor: colors.danger,
        borderColor: colors.danger,
    },
    favToggleText: {
        ...typography.caption,
        color: colors.danger,
        fontWeight: '700',
    },
    favToggleTextActive: {
        color: colors.white,
    },
    resetPill: {
        marginLeft: 'auto',
        paddingHorizontal: spacing.base,
        paddingVertical: 10,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.borderLight,
    },
    resetPillText: {
        ...typography.caption,
        color: colors.textSecondary,
        fontWeight: '700',
    },
    filterSection: {
        marginHorizontal: spacing.screenHorizontal,
        marginTop: spacing.base,
    },
    filterTitleRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: spacing.sm,
    },
    filterTitle: {
        ...typography.label,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    filterHint: {
        ...typography.caption,
        color: colors.textTertiary,
    },
    filterChip: {
        paddingHorizontal: spacing.base,
        paddingVertical: 10,
        borderRadius: 18,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.border,
        marginRight: spacing.sm,
    },
    filterChipActive: {
        backgroundColor: colors.primarySoft,
        borderColor: colors.primary,
    },
    filterChipText: {
        ...typography.caption,
        color: colors.textSecondary,
        fontWeight: '600',
    },
    filterChipTextActive: {
        color: colors.primary,
        fontWeight: '700',
    },
    resultsBanner: {
        marginHorizontal: spacing.screenHorizontal,
        marginTop: spacing.sm,
        marginBottom: spacing.base,
        paddingHorizontal: spacing.base,
        paddingVertical: spacing.md,
        borderRadius: 16,
        backgroundColor: colors.surface,
        borderWidth: 1,
        borderColor: colors.border,
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: spacing.base,
    },
    resultsLabel: {
        ...typography.caption,
        color: colors.textTertiary,
    },
    resultsTitle: {
        ...typography.caption,
        color: colors.textPrimary,
        fontWeight: '700',
    },
    resultsPill: {
        paddingHorizontal: spacing.sm,
        paddingVertical: 6,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.primarySoft,
    },
    resultsPillText: {
        ...typography.caption,
        color: colors.primaryDark,
        fontWeight: '700',
    },
    listContent: {
        paddingBottom: spacing['2xl'],
        flexGrow: 1,
    },
    modalOverlay: {
        flex: 1,
        backgroundColor: 'rgba(0,0,0,0.4)',
        justifyContent: 'flex-end',
    },
    modalContent: {
        backgroundColor: colors.surface,
        borderTopLeftRadius: spacing.radius.xl,
        borderTopRightRadius: spacing.radius.xl,
        padding: spacing.xl,
        paddingBottom: 40,
    },
    modalTitle: {
        ...typography.h3,
        color: colors.textPrimary,
        marginBottom: spacing.lg,
    },
    sortOption: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.md,
        paddingVertical: spacing.md,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    sortOptionActive: {
        backgroundColor: colors.primarySoft,
        marginHorizontal: -spacing.md,
        paddingHorizontal: spacing.md,
        borderRadius: spacing.radius.md,
    },
    sortOptionText: {
        ...typography.body,
        color: colors.textSecondary,
        flex: 1,
    },
    sortOptionTextActive: {
        color: colors.primary,
        fontWeight: '700',
    },
});

export default SearchScreen;
