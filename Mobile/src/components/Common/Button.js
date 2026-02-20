/**
 * Button Component
 * Reusable button with primary, secondary, outline, danger variants
 */

import React from 'react';
import { TouchableOpacity, Text, ActivityIndicator, StyleSheet } from 'react-native';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';

const Button = React.memo(({
    title,
    onPress,
    variant = 'primary',
    size = 'md',
    loading = false,
    disabled = false,
    icon,
    style,
    textStyle,
    ...props
}) => {
    const buttonStyles = getButtonStyles(variant, size);
    const isDisabled = disabled || loading;

    return (
        <TouchableOpacity
            disabled={isDisabled}
            onPress={onPress}
            activeOpacity={0.7}
            style={[
                styles.base,
                buttonStyles.button,
                isDisabled && styles.disabled,
                style,
            ]}
            {...props}
        >
            {loading ? (
                <ActivityIndicator
                    color={variant === 'outline' || variant === 'ghost' ? colors.primary : colors.white}
                    size="small"
                />
            ) : (
                <>
                    {icon}
                    <Text style={[buttonStyles.text, textStyle]}>{title}</Text>
                </>
            )}
        </TouchableOpacity>
    );
});

const getButtonStyles = (variant, size) => {
    const sizeStyles = {
        sm: { paddingVertical: 8, paddingHorizontal: 16, fontSize: 13 },
        md: { paddingVertical: spacing.buttonPaddingV, paddingHorizontal: spacing.buttonPaddingH, fontSize: 16 },
        lg: { paddingVertical: 16, paddingHorizontal: 32, fontSize: 18 },
    };

    const s = sizeStyles[size] || sizeStyles.md;

    const variants = {
        primary: {
            button: { backgroundColor: colors.primary, ...shadows.button, paddingVertical: s.paddingVertical, paddingHorizontal: s.paddingHorizontal },
            text: { color: colors.white, fontSize: s.fontSize, fontWeight: typography.weight.semibold },
        },
        secondary: {
            button: { backgroundColor: colors.primarySoft, paddingVertical: s.paddingVertical, paddingHorizontal: s.paddingHorizontal },
            text: { color: colors.primary, fontSize: s.fontSize, fontWeight: typography.weight.semibold },
        },
        outline: {
            button: { backgroundColor: 'transparent', borderWidth: 1.5, borderColor: colors.primary, paddingVertical: s.paddingVertical, paddingHorizontal: s.paddingHorizontal },
            text: { color: colors.primary, fontSize: s.fontSize, fontWeight: typography.weight.semibold },
        },
        danger: {
            button: { backgroundColor: colors.danger, paddingVertical: s.paddingVertical, paddingHorizontal: s.paddingHorizontal },
            text: { color: colors.white, fontSize: s.fontSize, fontWeight: typography.weight.semibold },
        },
        ghost: {
            button: { backgroundColor: 'transparent', paddingVertical: s.paddingVertical, paddingHorizontal: s.paddingHorizontal },
            text: { color: colors.primary, fontSize: s.fontSize, fontWeight: typography.weight.medium },
        },
        accent: {
            button: { backgroundColor: colors.accent, ...shadows.button, paddingVertical: s.paddingVertical, paddingHorizontal: s.paddingHorizontal },
            text: { color: colors.white, fontSize: s.fontSize, fontWeight: typography.weight.semibold },
        },
    };

    return variants[variant] || variants.primary;
};

const styles = StyleSheet.create({
    base: {
        borderRadius: spacing.buttonRadius,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 8,
    },
    disabled: {
        opacity: 0.5,
    },
});

Button.displayName = 'Button';

export default Button;
