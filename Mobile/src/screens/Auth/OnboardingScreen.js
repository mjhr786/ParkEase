/**
 * OnboardingScreen
 * First-launch walkthrough with swipeable slides
 */

import React, { useState, useRef, useCallback } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, Dimensions, Animated } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import cacheService from '../../services/storage/cacheService';
import { colors, spacing, typography } from '../../styles/globalStyles';

const { width: SCREEN_WIDTH } = Dimensions.get('window');

const SLIDES = [
    {
        id: '1',
        icon: 'search',
        title: 'Find Parking Instantly',
        description: 'Search and book nearby parking spaces in seconds. No more circling the block!',
        color: colors.primary,
        gradient: ['#4A90D9', '#357ABD'],
    },
    {
        id: '2',
        icon: 'location',
        title: 'List Your Space',
        description: 'Got a parking spot? List it and earn money when you\'re not using it.',
        color: colors.success,
        gradient: ['#34C759', '#28A745'],
    },
    {
        id: '3',
        icon: 'calendar',
        title: 'Manage Bookings',
        description: 'Approve requests, track bookings, check in/out — everything in one place.',
        color: colors.accent,
        gradient: ['#FF9500', '#E68A00'],
    },
    {
        id: '4',
        icon: 'rocket',
        title: 'Ready to Go!',
        description: 'Create your account and start parking smarter today.',
        color: '#6C63FF',
        gradient: ['#6C63FF', '#5A52E0'],
    },
];

const OnboardingScreen = ({ navigation }) => {
    const [currentIndex, setCurrentIndex] = useState(0);
    const flatListRef = useRef(null);
    const scrollX = useRef(new Animated.Value(0)).current;

    const onViewableItemsChanged = useRef(({ viewableItems }) => {
        if (viewableItems.length > 0) {
            setCurrentIndex(viewableItems[0].index);
        }
    }).current;

    const viewabilityConfig = useRef({ viewAreaCoveragePercentThreshold: 50 }).current;

    const handleNext = useCallback(() => {
        if (currentIndex < SLIDES.length - 1) {
            flatListRef.current?.scrollToIndex({ index: currentIndex + 1 });
        } else {
            handleGetStarted();
        }
    }, [currentIndex]);

    const handleGetStarted = useCallback(async () => {
        await cacheService.setOnboardingDone();
        navigation.navigate('Login');
    }, [navigation]);

    const handleSkip = useCallback(async () => {
        await cacheService.setOnboardingDone();
        navigation.navigate('Login');
    }, [navigation]);

    const renderSlide = ({ item, index }) => (
        <View style={[styles.slide, { width: SCREEN_WIDTH }]}>
            <LinearGradient colors={item.gradient} style={styles.iconCircle}>
                <Ionicons name={item.icon} size={60} color={colors.white} />
            </LinearGradient>
            <Text style={styles.slideTitle}>{item.title}</Text>
            <Text style={styles.slideDescription}>{item.description}</Text>
        </View>
    );

    const isLastSlide = currentIndex === SLIDES.length - 1;

    return (
        <SafeAreaView style={styles.container}>
            {/* Skip Button */}
            {!isLastSlide && (
                <TouchableOpacity style={styles.skipBtn} onPress={handleSkip}>
                    <Text style={styles.skipText}>Skip</Text>
                </TouchableOpacity>
            )}

            {/* Slides */}
            <FlatList
                ref={flatListRef}
                data={SLIDES}
                renderItem={renderSlide}
                keyExtractor={(item) => item.id}
                horizontal
                pagingEnabled
                bounces={false}
                showsHorizontalScrollIndicator={false}
                onScroll={Animated.event(
                    [{ nativeEvent: { contentOffset: { x: scrollX } } }],
                    { useNativeDriver: false }
                )}
                onViewableItemsChanged={onViewableItemsChanged}
                viewabilityConfig={viewabilityConfig}
            />

            {/* Bottom Controls */}
            <View style={styles.bottomSection}>
                {/* Dot Indicators */}
                <View style={styles.dotsRow}>
                    {SLIDES.map((_, index) => {
                        const inputRange = [
                            (index - 1) * SCREEN_WIDTH,
                            index * SCREEN_WIDTH,
                            (index + 1) * SCREEN_WIDTH,
                        ];
                        const dotWidth = scrollX.interpolate({
                            inputRange,
                            outputRange: [8, 24, 8],
                            extrapolate: 'clamp',
                        });
                        const dotOpacity = scrollX.interpolate({
                            inputRange,
                            outputRange: [0.3, 1, 0.3],
                            extrapolate: 'clamp',
                        });
                        return (
                            <Animated.View
                                key={index}
                                style={[styles.dot, { width: dotWidth, opacity: dotOpacity }]}
                            />
                        );
                    })}
                </View>

                {/* Action Button */}
                <TouchableOpacity
                    style={[styles.actionBtn, isLastSlide && styles.actionBtnFull]}
                    onPress={isLastSlide ? handleGetStarted : handleNext}
                    activeOpacity={0.8}
                >
                    {isLastSlide ? (
                        <Text style={styles.actionBtnText}>Get Started</Text>
                    ) : (
                        <Ionicons name="arrow-forward" size={24} color={colors.white} />
                    )}
                </TouchableOpacity>
            </View>
        </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.background,
    },
    skipBtn: {
        position: 'absolute',
        top: 60,
        right: spacing.screenHorizontal,
        zIndex: 10,
        paddingHorizontal: spacing.base,
        paddingVertical: spacing.sm,
    },
    skipText: {
        ...typography.body,
        color: colors.textTertiary,
        fontWeight: '600',
    },
    slide: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        paddingHorizontal: spacing.screenHorizontal * 2,
    },
    iconCircle: {
        width: 140,
        height: 140,
        borderRadius: 70,
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: spacing['2xl'],
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 8 },
        shadowOpacity: 0.2,
        shadowRadius: 16,
        elevation: 10,
    },
    slideTitle: {
        fontSize: 28,
        fontWeight: '800',
        color: colors.textPrimary,
        textAlign: 'center',
        marginBottom: spacing.md,
    },
    slideDescription: {
        ...typography.body,
        color: colors.textSecondary,
        textAlign: 'center',
        lineHeight: 24,
    },
    bottomSection: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingBottom: spacing['2xl'],
        alignItems: 'center',
    },
    dotsRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 8,
        marginBottom: spacing.xl,
    },
    dot: {
        height: 8,
        borderRadius: 4,
        backgroundColor: colors.primary,
    },
    actionBtn: {
        width: 60,
        height: 60,
        borderRadius: 30,
        backgroundColor: colors.primary,
        justifyContent: 'center',
        alignItems: 'center',
        shadowColor: colors.primary,
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.3,
        shadowRadius: 8,
        elevation: 6,
    },
    actionBtnFull: {
        width: '100%',
        borderRadius: spacing.radius.lg,
    },
    actionBtnText: {
        ...typography.h4,
        color: colors.white,
        fontWeight: '700',
    },
});

export default OnboardingScreen;
