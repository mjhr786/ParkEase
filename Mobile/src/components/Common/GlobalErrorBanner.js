import React, { useState, useEffect, useRef } from 'react';
import { View, Text, StyleSheet, Animated, Platform, TouchableOpacity } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography, shadows } from '../../styles/globalStyles';
import { EventBus } from '../../utils/EventBus';

/**
 * GlobalErrorBanner Component
 * 
 * Renders an auto-disappearing error banner at the top of the screen.
 * Listens for the 'SHOW_ERROR_BANNER' event triggered via EventBus.
 * 
 * @component
 * @example
 * // Trigger the banner from anywhere:
 * EventBus.emit('SHOW_ERROR_BANNER', { title: 'Network Error', message: 'Could not connect.' });
 * 
 * @returns {React.ReactElement|null} The error banner component
 */
const GlobalErrorBanner = () => {
    const [visible, setVisible] = useState(false);
    const [message, setMessage] = useState('Something went wrong. Please try again later.');
    const [title, setTitle] = useState('Oops!');
    
    // We use -150 to hide it fully off screen
    const translateY = useRef(new Animated.Value(-150)).current;
    const insets = useSafeAreaInsets();
    // Use a ref to keep track of the timer so we can clear it if another error comes in
    const timerRef = useRef(null);
    
    useEffect(() => {
        const handleShowError = (data) => {
            setTitle(data?.title || 'Connection Issue');
            setMessage(data?.message || 'We are having trouble connecting. Please try again later.');
            
            setVisible(true);
            
            // Clear any existing timeout
            if (timerRef.current) {
                clearTimeout(timerRef.current);
            }
            
            // Slide in
            Animated.spring(translateY, {
                toValue: 0,
                useNativeDriver: true,
                speed: 12,
                bounciness: 4,
            }).start();
            
            // Auto hide after 3.5 seconds
            timerRef.current = setTimeout(() => {
                hideBanner();
            }, 3500);
        };
        
        EventBus.on('SHOW_ERROR_BANNER', handleShowError);
        return () => EventBus.off('SHOW_ERROR_BANNER', handleShowError);
    }, [translateY]);
    
    const hideBanner = () => {
        Animated.timing(translateY, {
            toValue: -150,
            duration: 300,
            useNativeDriver: true,
        }).start(() => {
            setVisible(false);
        });
    };
    
    if (!visible) return null;
    
    const paddingTop = insets.top > 0 ? insets.top + 10 : (Platform.OS === 'android' ? 40 : 20);
    
    return (
        <Animated.View style={[styles.container, { transform: [{ translateY }], paddingTop }]}>
            <TouchableOpacity activeOpacity={0.9} onPress={hideBanner} style={styles.content}>
                <View style={styles.iconContainer}>
                    <Ionicons name="warning-outline" size={24} color={colors.white} />
                </View>
                <View style={styles.textContainer}>
                    <Text style={styles.title} numberOfLines={1}>{title}</Text>
                    <Text style={styles.message} numberOfLines={2}>{message}</Text>
                </View>
                <Ionicons name="close" size={20} color="rgba(255,255,255,0.6)" />
            </TouchableOpacity>
        </Animated.View>
    );
};

const styles = StyleSheet.create({
    container: {
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        zIndex: 99999,
        backgroundColor: colors.danger,
        paddingBottom: 16,
        paddingHorizontal: 20,
        ...shadows.lg,
    },
    content: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    iconContainer: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: 'rgba(255,255,255,0.2)',
        justifyContent: 'center',
        alignItems: 'center',
        marginRight: 12,
    },
    textContainer: {
        flex: 1,
        marginRight: 8,
    },
    title: {
        ...typography.label,
        color: colors.white,
        marginBottom: 2,
    },
    message: {
        ...typography.caption,
        color: 'rgba(255,255,255,0.9)',
        lineHeight: 18,
    },
});

export default GlobalErrorBanner;
