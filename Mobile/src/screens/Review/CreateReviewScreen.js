/**
 * CreateReviewScreen
 * Star rating input, title, comment form
 */

import React, { useState, useCallback } from 'react';
import { View, Text, TouchableOpacity, Alert, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { createReviewThunk } from '../../store/slices/reviewSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import StarRating from '../../components/Common/StarRating';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';

const CreateReviewScreen = ({ navigation, route }) => {
    const { parkingSpaceId } = route.params;
    const dispatch = useDispatch();
    const { createLoading } = useSelector((s) => s.review);

    const [rating, setRating] = useState(0);
    const [title, setTitle] = useState('');
    const [comment, setComment] = useState('');

    const handleSubmit = useCallback(async () => {
        if (rating === 0) {
            Alert.alert('Rating Required', 'Please select a star rating');
            return;
        }

        const result = await dispatch(createReviewThunk({
            parkingSpaceId,
            rating,
            title: title || undefined,
            comment: comment || undefined,
        }));

        if (!result.error) {
            Alert.alert('Thank You!', 'Your review has been submitted', [
                { text: 'OK', onPress: () => navigation.goBack() },
            ]);
        }
    }, [dispatch, parkingSpaceId, rating, title, comment, navigation]);

    return (
        <ScreenLayout scrollable>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Write Review</Text>
                <View style={{ width: 40 }} />
            </View>

            <View style={styles.content}>
                {/* Rating */}
                <Card style={styles.ratingCard}>
                    <Text style={styles.ratingPrompt}>How was your experience?</Text>
                    <StarRating rating={rating} onRatingChange={setRating} editable size={40} style={styles.stars} />
                    <Text style={styles.ratingLabel}>
                        {rating === 0 ? 'Tap a star to rate' : ['', 'Poor', 'Fair', 'Good', 'Great', 'Excellent'][rating]}
                    </Text>
                </Card>

                {/* Title */}
                <Input label="Review Title (optional)" value={title} onChangeText={setTitle} placeholder="Summarize your experience" leftIcon="create-outline" />

                {/* Comment */}
                <Input label="Comment (optional)" value={comment} onChangeText={setComment} placeholder="Share more details" multiline numberOfLines={4} />

                <Button title="Submit Review" onPress={handleSubmit} loading={createLoading} icon={<Ionicons name="send" size={20} color={colors.white} />} />
            </View>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: 60, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base },
    backBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, justifyContent: 'center', alignItems: 'center', ...shadows.sm },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    content: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['3xl'] },
    ratingCard: { alignItems: 'center', paddingVertical: spacing.xl },
    ratingPrompt: { ...typography.h3, color: colors.textPrimary, marginBottom: spacing.lg },
    stars: { marginBottom: spacing.md },
    ratingLabel: { ...typography.body, color: colors.textSecondary, fontWeight: '500' },
});

export default CreateReviewScreen;
