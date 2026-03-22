/**
 * FavoritesScreen
 * List of user's favorited parking spaces
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getFavoritesThunk, toggleFavoriteThunk } from '../../store/slices/favoriteSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency } from '../../utils/formatters';

const FavoriteItem = ({ item, onToggle, onPress }) => (
    <Card style={fStyles.card} onPress={onPress}>
        <View style={fStyles.row}>
            <View style={fStyles.iconWrap}>
                <Ionicons name="location" size={24} color={colors.primary} />
            </View>
            <View style={fStyles.info}>
                <Text style={fStyles.title} numberOfLines={1}>{item.title || item.parkingSpaceTitle}</Text>
                <Text style={fStyles.address} numberOfLines={1}>{item.address || item.parkingSpaceAddress}</Text>
                {item.pricePerHour != null && (
                    <Text style={fStyles.price}>{formatCurrency(item.pricePerHour)}/hr</Text>
                )}
            </View>
            <TouchableOpacity onPress={onToggle} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
                <Ionicons name="heart" size={24} color={colors.danger} />
            </TouchableOpacity>
        </View>
    </Card>
);

const fStyles = StyleSheet.create({
    card: { marginHorizontal: spacing.screenHorizontal },
    row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
    iconWrap: {
        width: 44,
        height: 44,
        borderRadius: 22,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
    },
    info: { flex: 1 },
    title: { ...typography.label, color: colors.textPrimary },
    address: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    price: { ...typography.caption, color: colors.primary, fontWeight: '600', marginTop: 2 },
});

const FavoritesScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { favorites, loading } = useSelector((state) => state.favorite);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getFavoritesThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getFavoritesThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleToggle = useCallback(
        (id) => dispatch(toggleFavoriteThunk(id)),
        [dispatch]
    );

    if (loading && !favorites.length) {
        return <LoadingScreen />;
    }

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <Text style={styles.title}>Favorites</Text>
                <View style={{ width: 24 }} />
            </View>
            <FlatList
                data={favorites}
                keyExtractor={(item) => (item.parkingSpaceId || item.id)?.toString()}
                renderItem={({ item }) => (
                    <FavoriteItem
                        item={item}
                        onToggle={() => handleToggle(item.parkingSpaceId || item.id)}
                        onPress={() => navigation.navigate('SearchTab', {
                            screen: 'ParkingDetail',
                            params: { parkingId: item.parkingSpaceId || item.id },
                        })}
                    />
                )}
                ListEmptyComponent={
                    <EmptyState
                        icon="heart-outline"
                        title="No favorites"
                        message="Heart a parking space to save it here"
                    />
                }
                refreshControl={
                    <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />
                }
                showsVerticalScrollIndicator={false}
                contentContainerStyle={{ paddingBottom: spacing['3xl'] }}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: 60,
        paddingBottom: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
    },
    title: { ...typography.h3, color: colors.textPrimary },
});

export default FavoritesScreen;
