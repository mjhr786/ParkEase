import React, { useEffect, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, RefreshControl, SafeAreaView, Image } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../../styles/globalStyles';
import { fetchFavoritesThunk, toggleFavoriteThunk } from '../../store/slices/favoriteSlice';

const FavoritesScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { favorites, isLoading } = useSelector(state => state.favorite);

    useEffect(() => {
        dispatch(fetchFavoritesThunk());
    }, [dispatch]);

    const handleRefresh = useCallback(() => {
        dispatch(fetchFavoritesThunk());
    }, [dispatch]);

    const handleRemoveFavorite = (parkingSpaceId) => {
        dispatch(toggleFavoriteThunk(parkingSpaceId));
    };

    const renderItem = ({ item }) => {
        return (
            <TouchableOpacity 
                style={styles.card}
                onPress={() => navigation.navigate('ParkingDetail', { id: item.id })}
            >
                {item.images && item.images.length > 0 ? (
                    <Image source={{ uri: item.images[0] }} style={styles.cardImage} />
                ) : (
                    <View style={[styles.cardImage, styles.placeholderImage]}>
                        <Ionicons name="image-outline" size={32} color={colors.textTertiary} />
                    </View>
                )}
                
                <View style={styles.cardContent}>
                    <Text style={styles.title} numberOfLines={1}>{item.title || item.name}</Text>
                    <Text style={styles.address} numberOfLines={2}>{item.address}</Text>
                    
                    <View style={styles.cardFooter}>
                        <Text style={styles.price}>${item.pricePerHour}/hr</Text>
                        <TouchableOpacity 
                            style={styles.favoriteButton}
                            onPress={() => handleRemoveFavorite(item.id)}
                        >
                            <Ionicons name="heart" size={24} color={colors.error} />
                        </TouchableOpacity>
                    </View>
                </View>
            </TouchableOpacity>
        );
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Saved Spots</Text>
                <View style={{ width: 34 }} />
            </View>

            <FlatList
                data={favorites}
                keyExtractor={(item) => item.id.toString()}
                renderItem={renderItem}
                refreshControl={
                    <RefreshControl 
                        refreshing={isLoading} 
                        onRefresh={handleRefresh} 
                        tintColor={colors.primary}
                    />
                }
                contentContainerStyle={styles.listContainer}
                ListEmptyComponent={
                    <View style={styles.emptyContainer}>
                        <Ionicons name="heart-outline" size={64} color={colors.borderLight} />
                        <Text style={styles.emptyText}>You haven't saved any spots yet.</Text>
                        <TouchableOpacity style={styles.exploreButton} onPress={() => navigation.navigate('SearchTab')}>
                            <Text style={styles.exploreButtonText}>Explore Parking</Text>
                        </TouchableOpacity>
                    </View>
                }
            />
        </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.background,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingHorizontal: 20,
        paddingVertical: 15,
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    backButton: {
        padding: 5,
    },
    headerTitle: {
        ...typography.h3,
        color: colors.text,
    },
    listContainer: {
        padding: 15,
        flexGrow: 1,
    },
    card: {
        flexDirection: 'row',
        backgroundColor: colors.surface,
        borderRadius: 12,
        marginBottom: 15,
        overflow: 'hidden',
        borderWidth: 1,
        borderColor: colors.borderLight,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.05,
        shadowRadius: 4,
        elevation: 2,
    },
    cardImage: {
        width: 100,
        height: 100,
    },
    placeholderImage: {
        backgroundColor: colors.background,
        justifyContent: 'center',
        alignItems: 'center',
    },
    cardContent: {
        flex: 1,
        padding: 12,
        justifyContent: 'space-between',
    },
    title: {
        ...typography.subtitle1,
        color: colors.text,
        marginBottom: 4,
    },
    address: {
        ...typography.body3,
        color: colors.textSecondary,
        marginBottom: 8,
    },
    cardFooter: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    price: {
        ...typography.subtitle2,
        color: colors.primary,
        fontWeight: '700',
    },
    favoriteButton: {
        padding: 4,
    },
    emptyContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        paddingVertical: 100,
    },
    emptyText: {
        ...typography.body1,
        color: colors.textTertiary,
        marginTop: 15,
        marginBottom: 25,
    },
    exploreButton: {
        backgroundColor: colors.primary,
        paddingHorizontal: 24,
        paddingVertical: 12,
        borderRadius: 25,
    },
    exploreButtonText: {
        ...typography.button,
        color: '#FFFFFF',
    }
});

export default FavoritesScreen;
