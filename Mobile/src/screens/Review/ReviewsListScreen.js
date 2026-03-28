/**
 * ReviewsListScreen
 * Full list of reviews for a parking space with rating summary
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getReviewsThunk } from '../../store/slices/reviewSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import StarRating from '../../components/Common/StarRating';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatDate } from '../../utils/formatters';

/* ───── Rating Summary ───── */
const RatingSummary = ({ reviews }) => {
    const total = reviews.length;
    if (total === 0) return null;

    const avg = reviews.reduce((sum, r) => sum + (r.rating || 0), 0) / total;
    const distribution = [5, 4, 3, 2, 1].map((star) => ({
        star,
        count: reviews.filter((r) => r.rating === star).length,
    }));

    return (
        <Card style={summaryStyles.card}>
            <View style={summaryStyles.row}>
                <View style={summaryStyles.avgCol}>
                    <Text style={summaryStyles.avgValue}>{avg.toFixed(1)}</Text>
                    <StarRating rating={avg} size={18} />
                    <Text style={summaryStyles.totalText}>{total} review{total !== 1 ? 's' : ''}</Text>
                </View>
                <View style={summaryStyles.barsCol}>
                    {distribution.map(({ star, count }) => (
                        <View key={star} style={summaryStyles.barRow}>
                            <Text style={summaryStyles.starLabel}>{star}</Text>
                            <Ionicons name="star" size={12} color={colors.warning} />
                            <View style={summaryStyles.barTrack}>
                                <View style={[summaryStyles.barFill, { width: `${total > 0 ? (count / total) * 100 : 0}%` }]} />
                            </View>
                            <Text style={summaryStyles.barCount}>{count}</Text>
                        </View>
                    ))}
                </View>
            </View>
        </Card>
    );
};

const summaryStyles = StyleSheet.create({
    card: { marginHorizontal: spacing.screenHorizontal },
    row: { flexDirection: 'row', gap: spacing.xl },
    avgCol: { alignItems: 'center', justifyContent: 'center' },
    avgValue: { fontSize: 40, fontWeight: '700', color: colors.textPrimary },
    totalText: { ...typography.caption, color: colors.textTertiary, marginTop: 4 },
    barsCol: { flex: 1, justifyContent: 'center', gap: 4 },
    barRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
    starLabel: { ...typography.caption, color: colors.textSecondary, width: 12, textAlign: 'right' },
    barTrack: { flex: 1, height: 6, backgroundColor: colors.borderLight, borderRadius: 3, overflow: 'hidden' },
    barFill: { height: 6, backgroundColor: colors.warning, borderRadius: 3 },
    barCount: { ...typography.caption, color: colors.textTertiary, width: 20 },
});

/* ───── Review Item ───── */
const ReviewItem = ({ review }) => (
    <Card style={reviewStyles.card}>
        <View style={reviewStyles.header}>
            <View style={reviewStyles.avatar}>
                <Text style={reviewStyles.avatarText}>
                    {(review.userName || '?').charAt(0).toUpperCase()}
                </Text>
            </View>
            <View style={reviewStyles.headerInfo}>
                <Text style={reviewStyles.name}>{review.userName}</Text>
                <Text style={reviewStyles.date}>{formatDate(review.createdAt)}</Text>
            </View>
            <StarRating rating={review.rating} size={14} />
        </View>
        {review.title && <Text style={reviewStyles.title}>{review.title}</Text>}
        {review.comment && <Text style={reviewStyles.comment}>{review.comment}</Text>}
        {/* Owner Response */}
        {review.ownerResponse && (
            <View style={reviewStyles.ownerResponseBox}>
                <View style={reviewStyles.ownerResponseHeader}>
                    <Ionicons name="business-outline" size={14} color={colors.primary} />
                    <Text style={reviewStyles.ownerResponseLabel}>Owner Response</Text>
                </View>
                <Text style={reviewStyles.ownerResponseText}>{review.ownerResponse}</Text>
            </View>
        )}
    </Card>
);

const reviewStyles = StyleSheet.create({
    card: { marginHorizontal: spacing.screenHorizontal },
    header: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
    avatar: {
        width: 36, height: 36, borderRadius: 18,
        backgroundColor: colors.primarySoft, justifyContent: 'center', alignItems: 'center',
    },
    avatarText: { color: colors.primary, fontSize: 14, fontWeight: '700' },
    headerInfo: { flex: 1 },
    name: { ...typography.label, color: colors.textPrimary },
    date: { ...typography.caption, color: colors.textTertiary, marginTop: 1 },
    title: { ...typography.label, color: colors.textPrimary, marginTop: spacing.sm },
    comment: { ...typography.body, color: colors.textSecondary, marginTop: spacing.xs, lineHeight: 22 },
    ownerResponseBox: {
        marginTop: spacing.md, backgroundColor: colors.primarySoft,
        padding: spacing.md, borderRadius: spacing.radius.md, borderLeftWidth: 3, borderLeftColor: colors.primary,
    },
    ownerResponseHeader: { flexDirection: 'row', alignItems: 'center', gap: 4, marginBottom: 4 },
    ownerResponseLabel: { ...typography.caption, color: colors.primary, fontWeight: '600' },
    ownerResponseText: { ...typography.bodySmall, color: colors.textSecondary, lineHeight: 20 },
});

/* ───── Main Screen ───── */
const ReviewsListScreen = ({ navigation, route }) => {
    const { parkingSpaceId, parkingTitle } = route.params;
    const dispatch = useDispatch();
    const { reviews, loading } = useSelector((state) => state.review);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getReviewsThunk(parkingSpaceId));
    }, [dispatch, parkingSpaceId]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getReviewsThunk(parkingSpaceId));
        setRefreshing(false);
    }, [dispatch, parkingSpaceId]);

    if (loading && !reviews.length) {
        return <LoadingScreen />;
    }

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <View style={styles.headerCenter}>
                    <Text style={styles.headerTitle}>Reviews</Text>
                    {parkingTitle && (
                        <Text style={styles.headerSubtitle} numberOfLines={1}>{parkingTitle}</Text>
                    )}
                </View>
                <View style={{ width: 40 }} />
            </View>

            <FlatList
                data={reviews}
                keyExtractor={(item) => item.id?.toString()}
                ListHeaderComponent={<RatingSummary reviews={reviews} />}
                renderItem={({ item }) => <ReviewItem review={item} />}
                ListEmptyComponent={
                    <EmptyState
                        icon="star-outline"
                        title="No reviews yet"
                        message="Be the first to leave a review!"
                    />
                }
                refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                showsVerticalScrollIndicator={false}
                contentContainerStyle={{ paddingBottom: spacing['3xl'] }}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
        paddingTop: spacing.md, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base,
    },
    backBtn: {
        width: 40, height: 40, borderRadius: 20,
        backgroundColor: colors.surface, justifyContent: 'center', alignItems: 'center',
        ...shadows.sm,
    },
    headerCenter: { flex: 1, alignItems: 'center' },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    headerSubtitle: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
});

export default ReviewsListScreen;
