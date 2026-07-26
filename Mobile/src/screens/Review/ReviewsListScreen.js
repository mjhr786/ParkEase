import React, { useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, SafeAreaView, ActivityIndicator } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../../styles/globalStyles';
import { getReviewsThunk, clearReviews } from '../../store/slices/reviewSlice';
import StarRating from '../../components/Common/StarRating';

const ReviewsListScreen = ({ route, navigation }) => {
    // We expect parkingSpaceId passed via route params
    const parkingSpaceId = route?.params?.parkingSpaceId || route?.params?.id;
    
    const dispatch = useDispatch();
    const { reviews, loading, error } = useSelector(state => state.review);

    useEffect(() => {
        if (parkingSpaceId) {
            dispatch(getReviewsThunk(parkingSpaceId));
        }

        // Cleanup on unmount
        return () => {
            dispatch(clearReviews());
        };
    }, [dispatch, parkingSpaceId]);

    const renderItem = ({ item }) => {
        return (
            <View style={styles.reviewCard}>
                <View style={styles.reviewHeader}>
                    <View style={styles.reviewerInfo}>
                        <View style={styles.avatarPlaceholder}>
                            <Text style={styles.avatarText}>
                                {item.userName ? item.userName.charAt(0).toUpperCase() : 'A'}
                            </Text>
                        </View>
                        <View>
                            <Text style={styles.reviewerName}>{item.userName || 'Anonymous User'}</Text>
                            <Text style={styles.reviewDate}>{new Date(item.createdAt).toLocaleDateString()}</Text>
                        </View>
                    </View>
                    <StarRating rating={item.rating} size={16} />
                </View>
                {item.comment ? (
                    <Text style={styles.reviewComment}>{item.comment}</Text>
                ) : null}
            </View>
        );
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Reviews</Text>
                <View style={{ width: 34 }} />
            </View>

            {loading ? (
                <View style={styles.centerContainer}>
                    <ActivityIndicator size="large" color={colors.primary} />
                </View>
            ) : error ? (
                <View style={styles.centerContainer}>
                    <Text style={styles.errorText}>{error}</Text>
                </View>
            ) : (
                <FlatList
                    data={reviews}
                    keyExtractor={(item, index) => item.id ? item.id.toString() : index.toString()}
                    renderItem={renderItem}
                    contentContainerStyle={styles.listContainer}
                    ListEmptyComponent={
                        <View style={styles.emptyContainer}>
                            <Ionicons name="chatbubbles-outline" size={64} color={colors.borderLight} />
                            <Text style={styles.emptyText}>No reviews yet.</Text>
                        </View>
                    }
                />
            )}
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
    reviewCard: {
        backgroundColor: colors.surface,
        borderRadius: 12,
        padding: 15,
        marginBottom: 15,
        borderWidth: 1,
        borderColor: colors.borderLight,
    },
    reviewHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: 10,
    },
    reviewerInfo: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    avatarPlaceholder: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: colors.primary + '20',
        justifyContent: 'center',
        alignItems: 'center',
        marginRight: 10,
    },
    avatarText: {
        ...typography.h4,
        color: colors.primary,
        fontWeight: 'bold',
    },
    reviewerName: {
        ...typography.subtitle1,
        color: colors.text,
    },
    reviewDate: {
        ...typography.body3,
        color: colors.textTertiary,
    },
    reviewComment: {
        ...typography.body1,
        color: colors.textSecondary,
        lineHeight: 22,
    },
    centerContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
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
    },
    errorText: {
        ...typography.body1,
        color: colors.error,
        paddingHorizontal: 20,
        textAlign: 'center',
    },
});

export default ReviewsListScreen;
