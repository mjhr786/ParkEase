/**
 * SwipeableRow
 * Custom implementation of swipe-to-reveal using PanResponder and Animated
 */

import React, { useRef, useState } from 'react';
import {
    Animated,
    PanResponder,
    StyleSheet,
    View,
    TouchableOpacity,
    Dimensions,
    ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, shadows } from '../../styles/globalStyles';

const { width: SCREEN_WIDTH } = Dimensions.get('window');
const SWIPE_THRESHOLD = -60; // Max swipe distance to reveal delete button
const SNAP_THRESHOLD = -40; // Snap to open if swiped past this

const SwipeableRow = ({ children, onDelete, isDeleting, style }) => {
    const translateX = useRef(new Animated.Value(0)).current;
    const [isOpen, setIsOpen] = useState(false);

    const panResponder = useRef(
        PanResponder.create({
            onStartShouldSetPanResponder: () => false,
            onMoveShouldSetPanResponder: (_, gestureState) => {
                // Only respond to horizontal swipes
                return Math.abs(gestureState.dx) > Math.abs(gestureState.dy) && Math.abs(gestureState.dx) > 10;
            },
            onPanResponderMove: (_, gestureState) => {
                let newX = gestureState.dx;
                if (isOpen) {
                    newX = SWIPE_THRESHOLD + gestureState.dx;
                }
                
                // Limit swipe to the left (negative X) and stop at SWIPE_THRESHOLD * 1.5 for bounce feel
                if (newX > 0) newX = 0;
                if (newX < SWIPE_THRESHOLD * 1.5) newX = SWIPE_THRESHOLD * 1.5;
                
                translateX.setValue(newX);
            },
            onPanResponderRelease: (_, gestureState) => {
                const currentX = isOpen ? SWIPE_THRESHOLD + gestureState.dx : gestureState.dx;
                
                if (currentX < SNAP_THRESHOLD) {
                    // Snap to Open
                    Animated.spring(translateX, {
                        toValue: SWIPE_THRESHOLD,
                        useNativeDriver: true,
                        bounciness: 8,
                    }).start();
                    setIsOpen(true);
                } else {
                    // Snap to Closed
                    Animated.spring(translateX, {
                        toValue: 0,
                        useNativeDriver: true,
                        friction: 5,
                    }).start();
                    setIsOpen(false);
                }
            },
        })
    ).current;

    const close = () => {
        Animated.spring(translateX, {
            toValue: 0,
            useNativeDriver: true,
        }).start();
        setIsOpen(false);
    };

    const handleDelete = () => {
        onDelete();
        close();
    };

    return (
        <View style={[styles.container, style]}>
            {/* Background Actions Layer */}
            <View style={styles.actionsContainer}>
                <TouchableOpacity 
                    style={[styles.deleteButton, isDeleting && styles.disabledButton]} 
                    onPress={handleDelete}
                    disabled={isDeleting}
                    activeOpacity={0.7}
                >
                    {isDeleting ? (
                        <ActivityIndicator color={colors.white} size="small" />
                    ) : (
                        <Ionicons name="trash-outline" size={24} color={colors.white} />
                    )}
                </TouchableOpacity>
            </View>

            {/* Foreground Content Layer */}
            <Animated.View
                style={[
                    styles.content,
                    { transform: [{ translateX }] }
                ]}
                {...panResponder.panHandlers}
            >
                {children}
                {isDeleting && (
                    <View style={styles.deletingOverlay}>
                        <ActivityIndicator color={colors.primary} size="small" />
                    </View>
                )}
            </Animated.View>
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        position: 'relative',
        backgroundColor: colors.danger,
        borderRadius: spacing.cardRadius,
        overflow: 'hidden',
    },
    actionsContainer: {
        position: 'absolute',
        top: 0,
        bottom: 0,
        right: 0,
        width: Math.abs(SWIPE_THRESHOLD),
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.danger,
    },
    deleteButton: {
        width: '100%',
        height: '100%',
        justifyContent: 'center',
        alignItems: 'center',
    },
    disabledButton: {
        opacity: 0.5,
    },
    content: {
        backgroundColor: colors.background,
    },
    deletingOverlay: {
        ...StyleSheet.absoluteFillObject,
        backgroundColor: 'rgba(255, 255, 255, 0.7)',
        justifyContent: 'center',
        alignItems: 'center',
        zIndex: 10,
    },
});

export default SwipeableRow;
