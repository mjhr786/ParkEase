/**
 * ParkingDetailScreen
 * Hero image design with sticky Book Now bar
 * Ownership-aware: own listings show manage options
 */

import React, { useEffect, useState, useCallback } from 'react';
import { EventBus } from '../../utils/EventBus';
import {
    View, Text, ScrollView, TouchableOpacity, StyleSheet, Image, Dimensions, Share,
} from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { getParkingDetailThunk } from '../../store/slices/parkingSlice';
import { getReviewsThunk } from '../../store/slices/reviewSlice';
import { toggleFavoriteThunk } from '../../store/slices/favoriteSlice';
import { useAuth } from '../../hooks/useAuth';
import StarRating from '../../components/Common/StarRating';
import { DetailSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { ParkingTypeLabels } from '../../utils/constants';
import chatService from '../../services/chat/chatService';

const { width } = Dimensions.get('window');
const HERO_HEIGHT = 300;

const ParkingDetailScreen = ({ navigation, route }) => {
    const { parkingId } = route.params;
    const insets = useSafeAreaInsets();
    const dispatch = useDispatch();
    const { user } = useAuth();
    const { selectedParking: parking, detailLoading } = useSelector((s) => s.parking);
    const { reviews } = useSelector((s) => s.review);
    const [chatLoading, setChatLoading] = useState(false);
    const [isFavorited, setIsFavorited] = useState(false);
    const [favLoading, setFavLoading] = useState(false);

    useEffect(() => {
        dispatch(getParkingDetailThunk(parkingId));
        dispatch(getReviewsThunk(parkingId));
    }, [dispatch, parkingId]);

    useEffect(() => {
        if (parking?.isFavorited !== undefined) setIsFavorited(parking.isFavorited);
    }, [parking?.isFavorited]);

    const isOwnListing = parking?.ownerId === user?.id || parking?.userId === user?.id;

    const handleToggleFavorite = useCallback(async () => {
        setFavLoading(true);
        try {
            const result = await dispatch(toggleFavoriteThunk(parkingId)).unwrap();
            setIsFavorited(result.isFavorited ?? !isFavorited);
        } catch {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Could not update favorite.' });
        } finally {
            setFavLoading(false);
        }
    }, [dispatch, parkingId, isFavorited]);

    const handleChatWithOwner = useCallback(async () => {
        setChatLoading(true);
        try {
            const existing = await chatService.findConversationByParkingSpace(parkingId);
            if (existing) {
                navigation.navigate('ChatScreen', {
                    conversationId: existing.id,
                    parkingSpaceId: parkingId,
                    participantName: existing.otherParticipantName,
                    parkingTitle: existing.parkingSpaceTitle,
                });
            } else {
                navigation.navigate('ChatScreen', {
                    conversationId: null,
                    parkingSpaceId: parkingId,
                    participantName: parking?.ownerName || 'Owner',
                    parkingTitle: parking?.title || 'Parking Space',
                });
            }
        } catch {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Could not open chat.' });
        } finally {
            setChatLoading(false);
        }
    }, [parkingId, parking, navigation]);

    const handleShare = useCallback(async () => {
        try {
            await Share.share({
                message: `Check out "${parking?.title}" on ParkEase!\n📍 ${parking?.address}, ${parking?.city}\n💰 ${formatCurrency(parking?.hourlyRate)}/hr\n\nDownload ParkEase to book this spot.`,
                title: parking?.title || 'ParkEase Parking Spot',
            });
        } catch (error) {
            // User cancelled or error
        }
    }, [parking]);

    // Improve UI stability: Only show detail if it matches the requested ID and isn't loading
    if (detailLoading || !parking || parking.id !== parkingId) {
        return (
            <View style={styles.screen}>
                <TouchableOpacity
                    style={[styles.heroBtn, styles.backBtn, { top: insets.top + 8, zIndex: 10 }]}
                    onPress={() => navigation.goBack()}
                >
                    <Ionicons name="chevron-back" size={22} color={colors.white} />
                </TouchableOpacity>
                <DetailSkeleton />
            </View>
        );
    }

    const hasImage = parking.images?.length > 0;
    const typeLabel = ParkingTypeLabels[parking.parkingType] || 'Parking';

    return (
        <View style={styles.screen}>
            <ScrollView
                showsVerticalScrollIndicator={false}
                contentContainerStyle={{ paddingBottom: 120 }}
            >
                {/* Hero Image */}
                <View style={styles.heroContainer}>
                    {hasImage ? (
                        <Image source={{ uri: parking.images[0] }} style={styles.heroImage} />
                    ) : (
                        <View style={styles.heroPlaceholder}>
                            <Ionicons name="car" size={60} color={colors.lightGray} />
                        </View>
                    )}

                    {/* Overlay Buttons */}
                    <TouchableOpacity
                        style={[styles.heroBtn, styles.backBtn, { top: insets.top + 8 }]}
                        onPress={() => navigation.goBack()}
                    >
                        <Ionicons name="chevron-back" size={22} color={colors.white} />
                    </TouchableOpacity>

                    <View style={[styles.heroTopRight, { top: insets.top + 8 }]}>
                        <TouchableOpacity style={styles.heroBtn} onPress={handleShare}>
                            <Ionicons name="share-outline" size={20} color={colors.white} />
                        </TouchableOpacity>
                        <TouchableOpacity
                            style={styles.heroBtn}
                            onPress={handleToggleFavorite}
                            disabled={favLoading}
                        >
                            <Ionicons
                                name={isFavorited ? 'heart' : 'heart-outline'}
                                size={20}
                                color={isFavorited ? '#EF4444' : colors.white}
                            />
                        </TouchableOpacity>
                    </View>

                    {/* Price Badge */}
                    <View style={styles.heroPriceBadge}>
                        <Text style={styles.heroPriceLabel}>STARTING FROM</Text>
                        <View style={styles.heroPriceRow}>
                            <Text style={styles.heroPriceValue}>
                                {formatCurrency(parking.hourlyRate)}
                            </Text>
                            <Text style={styles.heroPriceUnit}>/hr</Text>
                        </View>
                    </View>
                </View>

                {/* Content */}
                <View style={styles.content}>
                    {/* Type + Rating */}
                    <View style={styles.typeRatingRow}>
                        <View style={styles.typeBadge}>
                            <Text style={styles.typeBadgeText}>{typeLabel.toUpperCase()}</Text>
                        </View>
                        <View style={styles.ratingRow}>
                            <Ionicons name="star" size={16} color="#F59E0B" />
                            <Text style={styles.ratingText}>
                                {parking.averageRating?.toFixed(1) || '0.0'}
                            </Text>
                            <Text style={styles.ratingCount}>({parking.totalReviews})</Text>
                        </View>
                    </View>

                    {/* Title */}
                    <Text style={styles.title}>{parking.title}</Text>

                    {/* Address */}
                    <View style={styles.addressRow}>
                        <Ionicons name="location-outline" size={16} color={colors.textTertiary} />
                        <Text style={styles.addressText}>
                            {parking.address}, {parking.city}
                        </Text>
                    </View>

                    {/* Own listing banner */}
                    {isOwnListing && (
                        <View style={styles.ownerBanner}>
                            <Ionicons name="shield-checkmark" size={16} color={colors.primary} />
                            <Text style={styles.ownerBannerText}>This is your listing</Text>
                        </View>
                    )}

                    {/* Owner Card */}
                    {!isOwnListing && (
                        <TouchableOpacity style={styles.ownerCard} onPress={handleChatWithOwner}>
                            <View style={styles.ownerAvatar}>
                                <Ionicons name="person" size={20} color={colors.white} />
                            </View>
                            <View style={styles.ownerInfo}>
                                <Text style={styles.ownerName}>
                                    {parking.ownerName || 'Space Owner'}
                                </Text>
                                <Text style={styles.ownerSince}>
                                    Lister since {parking.createdAt ? new Date(parking.createdAt).getFullYear() : '2024'}
                                </Text>
                            </View>
                            <View style={styles.chatActionBtn}>
                                <Ionicons name="chatbubble-ellipses-outline" size={18} color={colors.primary} />
                                <Text style={styles.chatActionText}>Chat</Text>
                            </View>
                        </TouchableOpacity>
                    )}

                    {/* Description */}
                    {parking.description && (
                        <View style={styles.section}>
                            <Text style={styles.sectionTitle}>About this space</Text>
                            <Text style={styles.description}>{parking.description}</Text>
                        </View>
                    )}

                    {/* Features & Amenities */}
                    {parking.amenities?.length > 0 && (
                        <View style={styles.section}>
                            <Text style={styles.sectionTitle}>Features & Amenities</Text>
                            <View style={styles.amenitiesGrid}>
                                {parking.amenities.map((amenity, idx) => (
                                    <View key={idx} style={styles.amenityChip}>
                                        <Ionicons name="checkmark" size={16} color={colors.primary} />
                                        <Text style={styles.amenityText}>{amenity}</Text>
                                    </View>
                                ))}
                            </View>
                        </View>
                    )}

                    {/* Quick Info */}
                    <View style={styles.quickInfoRow}>
                        <View style={styles.quickInfoItem}>
                            <Ionicons name="car-outline" size={18} color={colors.primary} />
                            <Text style={styles.quickInfoText}>
                                {parking.availableSpots}/{parking.totalSpots} spots
                            </Text>
                        </View>
                        <View style={styles.quickInfoItem}>
                            <Ionicons name="time-outline" size={18} color={colors.primary} />
                            <Text style={styles.quickInfoText}>
                                {parking.is24Hours ? '24/7' : 'Limited hours'}
                            </Text>
                        </View>
                    </View>

                    {/* Reviews */}
                    <View style={styles.section}>
                        <View style={styles.sectionRow}>
                            <Text style={styles.sectionTitle}>
                                Reviews ({reviews.length})
                            </Text>
                            <TouchableOpacity onPress={() => navigation.navigate('ReviewsList', {
                                parkingSpaceId: parkingId,
                                parkingTitle: parking?.title,
                            })}>
                                <Text style={styles.seeAllText}>See All</Text>
                            </TouchableOpacity>
                        </View>
                        {reviews.length > 0 ? (
                            reviews.slice(0, 2).map((review) => (
                                <View key={review.id} style={styles.reviewItem}>
                                    <View style={styles.reviewHeader}>
                                        <Text style={styles.reviewerName}>{review.userName}</Text>
                                        <StarRating rating={review.rating} size={14} />
                                    </View>
                                    {review.comment && (
                                        <Text style={styles.reviewComment}>{review.comment}</Text>
                                    )}
                                </View>
                            ))
                        ) : (
                            <Text style={{ fontSize: 14, color: colors.textTertiary, marginTop: 4 }}>
                                No reviews yet. Be the first to book!
                            </Text>
                        )}
                    </View>
                </View>
            </ScrollView>

            {/* Sticky Bottom Bar */}
            <View style={[styles.bottomBar, { paddingBottom: Math.max(insets.bottom, 16) }]}>
                {isOwnListing ? (
                    <>
                        <View style={styles.bottomPriceCol}>
                            <Text style={styles.bottomPriceLabel}>Your Listing</Text>
                        </View>
                        <TouchableOpacity
                            style={[styles.bookBtn, { backgroundColor: colors.textPrimary }]}
                            onPress={() => navigation.navigate('CreateParking', { editData: parking })}
                        >
                            <Text style={styles.bookBtnText}>Edit Listing</Text>
                        </TouchableOpacity>
                    </>
                ) : (
                    <>
                        <View style={styles.bottomPriceCol}>
                            <Text style={styles.bottomPriceLabel}>Total Price</Text>
                            <Text style={styles.bottomPriceValue}>
                                {formatCurrency(parking.hourlyRate)}
                            </Text>
                        </View>
                        <TouchableOpacity
                            style={styles.bookBtn}
                            onPress={() => navigation.navigate('BookParking', { parkingId: parking.id })}
                        >
                            <Text style={styles.bookBtnText}>Book Now</Text>
                        </TouchableOpacity>
                    </>
                )}
            </View>
        </View>
    );
};

const styles = StyleSheet.create({
    screen: {
        flex: 1,
        backgroundColor: colors.white,
    },
    // Hero
    heroContainer: {
        height: HERO_HEIGHT,
        position: 'relative',
    },
    heroImage: {
        width: '100%',
        height: '100%',
        resizeMode: 'cover',
    },
    heroPlaceholder: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: '#E2E8F0',
    },
    heroBtn: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: 'rgba(0,0,0,0.3)',
        justifyContent: 'center',
        alignItems: 'center',
    },
    backBtn: {
        position: 'absolute',
        left: 16,
    },
    heroTopRight: {
        position: 'absolute',
        right: 16,
        flexDirection: 'row',
        gap: 8,
    },
    heroPriceBadge: {
        position: 'absolute',
        bottom: 16,
        right: 16,
        backgroundColor: colors.white,
        borderRadius: 12,
        paddingHorizontal: 14,
        paddingVertical: 8,
        ...shadows.md,
    },
    heroPriceLabel: {
        fontSize: 10,
        fontWeight: '600',
        color: colors.textTertiary,
        letterSpacing: 0.5,
    },
    heroPriceRow: {
        flexDirection: 'row',
        alignItems: 'baseline',
    },
    heroPriceValue: {
        fontSize: 22,
        fontWeight: '800',
        color: colors.textPrimary,
    },
    heroPriceUnit: {
        fontSize: 14,
        fontWeight: '500',
        color: colors.textTertiary,
        marginLeft: 2,
    },
    // Content
    content: {
        padding: 20,
    },
    typeRatingRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 12,
        marginBottom: 8,
    },
    typeBadge: {
        backgroundColor: '#EBF5FF',
        paddingHorizontal: 12,
        paddingVertical: 5,
        borderRadius: 8,
    },
    typeBadgeText: {
        fontSize: 12,
        fontWeight: '700',
        color: colors.primary,
        letterSpacing: 0.5,
    },
    ratingRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
    },
    ratingText: {
        fontSize: 15,
        fontWeight: '700',
        color: colors.textPrimary,
    },
    ratingCount: {
        fontSize: 14,
        color: colors.textTertiary,
    },
    title: {
        fontSize: 26,
        fontWeight: '800',
        color: colors.textPrimary,
        lineHeight: 32,
        marginBottom: 8,
    },
    addressRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 4,
        marginBottom: 20,
    },
    addressText: {
        fontSize: 14,
        color: colors.textSecondary,
        flex: 1,
    },
    ownerBanner: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 8,
        backgroundColor: '#EBF5FF',
        padding: 12,
        borderRadius: 12,
        marginBottom: 20,
    },
    ownerBannerText: {
        fontSize: 14,
        fontWeight: '600',
        color: colors.primary,
    },
    // Owner Card
    ownerCard: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.background,
        borderRadius: 14,
        padding: 14,
        marginBottom: 24,
    },
    ownerAvatar: {
        width: 48,
        height: 48,
        borderRadius: 24,
        backgroundColor: colors.primary,
        justifyContent: 'center',
        alignItems: 'center',
    },
    ownerInfo: {
        flex: 1,
        marginLeft: 12,
    },
    ownerName: {
        fontSize: 16,
        fontWeight: '700',
        color: colors.textPrimary,
    },
    ownerSince: {
        fontSize: 13,
        color: colors.textTertiary,
        marginTop: 2,
    },
    chatActionBtn: {
        flexDirection: 'row', alignItems: 'center', gap: 4,
        paddingHorizontal: 12, paddingVertical: 6,
        backgroundColor: colors.primarySoft, borderRadius: 16,
    },
    chatActionText: { fontSize: 13, fontWeight: '600', color: colors.primary },
    // Sections
    section: {
        marginBottom: 24,
    },
    sectionTitle: {
        fontSize: 18,
        fontWeight: '700',
        color: colors.textPrimary,
        marginBottom: 12,
    },
    sectionRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 12,
    },
    seeAllText: {
        fontSize: 14,
        fontWeight: '600',
        color: colors.primary,
    },
    description: {
        fontSize: 15,
        color: colors.textSecondary,
        lineHeight: 24,
    },
    // Amenities
    amenitiesGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: 10,
    },
    amenityChip: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingHorizontal: 14,
        paddingVertical: 10,
        borderRadius: 10,
        backgroundColor: colors.background,
        borderWidth: 1,
        borderColor: colors.border,
    },
    amenityText: {
        fontSize: 14,
        fontWeight: '500',
        color: colors.textPrimary,
    },
    // Quick Info
    quickInfoRow: {
        flexDirection: 'row',
        gap: 16,
        marginBottom: 24,
    },
    quickInfoItem: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        backgroundColor: colors.background,
        paddingHorizontal: 14,
        paddingVertical: 10,
        borderRadius: 10,
    },
    quickInfoText: {
        fontSize: 13,
        fontWeight: '600',
        color: colors.textSecondary,
    },
    // Reviews
    reviewItem: {
        paddingVertical: 12,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    reviewHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    reviewerName: {
        fontSize: 15,
        fontWeight: '600',
        color: colors.textPrimary,
    },
    reviewComment: {
        fontSize: 14,
        color: colors.textSecondary,
        marginTop: 6,
        lineHeight: 20,
    },
    // Bottom Bar
    bottomBar: {
        position: 'absolute',
        bottom: 0,
        left: 0,
        right: 0,
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.white,
        paddingHorizontal: 20,
        paddingTop: 16,
        borderTopWidth: 1,
        borderTopColor: colors.borderLight,
        ...shadows.lg,
    },
    bottomPriceCol: {
        flex: 1,
    },
    bottomPriceLabel: {
        fontSize: 12,
        color: colors.textTertiary,
        fontWeight: '500',
    },
    bottomPriceValue: {
        fontSize: 24,
        fontWeight: '800',
        color: colors.primary,
    },
    bookBtn: {
        backgroundColor: colors.primary,
        borderRadius: 14,
        paddingVertical: 16,
        paddingHorizontal: 40,
    },
    bookBtnText: {
        fontSize: 16,
        fontWeight: '700',
        color: colors.white,
    },
});

export default ParkingDetailScreen;
