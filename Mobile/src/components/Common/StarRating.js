/**
 * StarRating Component
 * Display and input component for star ratings
 */

import React from 'react';
import { View, TouchableOpacity, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing } from '../../styles/globalStyles';

const StarRating = ({
    rating = 0,
    maxStars = 5,
    size = 20,
    editable = false,
    onRatingChange,
    color = colors.accent,
    style,
}) => {
    return (
        <View style={[styles.container, style]}>
            {Array.from({ length: maxStars }, (_, index) => {
                const starNumber = index + 1;
                const filled = starNumber <= Math.floor(rating);
                const halfFilled = !filled && starNumber <= Math.ceil(rating) && rating % 1 >= 0.5;

                return (
                    <TouchableOpacity
                        key={index}
                        disabled={!editable}
                        onPress={() => editable && onRatingChange?.(starNumber)}
                        style={styles.star}
                    >
                        <Ionicons
                            name={filled ? 'star' : halfFilled ? 'star-half' : 'star-outline'}
                            size={size}
                            color={filled || halfFilled ? color : colors.lightGray}
                        />
                    </TouchableOpacity>
                );
            })}
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    star: {
        marginRight: 2,
    },
});

export default StarRating;
