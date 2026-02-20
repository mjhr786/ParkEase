/**
 * Input Component
 * Text input with label, error state, icon support
 */

import React, { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';

const Input = ({
    label,
    value,
    onChangeText,
    placeholder,
    error,
    secureTextEntry = false,
    keyboardType = 'default',
    autoCapitalize = 'sentences',
    multiline = false,
    numberOfLines = 1,
    leftIcon,
    editable = true,
    style,
    inputStyle,
    ...props
}) => {
    const [focused, setFocused] = useState(false);
    const [showPassword, setShowPassword] = useState(false);

    return (
        <View style={[styles.container, style]}>
            {label && <Text style={styles.label}>{label}</Text>}
            <View
                style={[
                    styles.inputContainer,
                    focused && styles.inputFocused,
                    error && styles.inputError,
                    !editable && styles.inputDisabled,
                    multiline && { height: numberOfLines * 40 },
                ]}
            >
                {leftIcon && (
                    <Ionicons
                        name={leftIcon}
                        size={20}
                        color={focused ? colors.primary : colors.textTertiary}
                        style={styles.leftIcon}
                    />
                )}
                <TextInput
                    value={value}
                    onChangeText={onChangeText}
                    placeholder={placeholder}
                    placeholderTextColor={colors.textTertiary}
                    secureTextEntry={secureTextEntry && !showPassword}
                    keyboardType={keyboardType}
                    autoCapitalize={autoCapitalize}
                    multiline={multiline}
                    numberOfLines={numberOfLines}
                    editable={editable}
                    onFocus={() => setFocused(true)}
                    onBlur={() => setFocused(false)}
                    style={[
                        styles.input,
                        multiline && styles.multilineInput,
                        inputStyle,
                    ]}
                    {...props}
                />
                {secureTextEntry && (
                    <TouchableOpacity onPress={() => setShowPassword(!showPassword)} style={styles.eyeIcon}>
                        <Ionicons
                            name={showPassword ? 'eye-off-outline' : 'eye-outline'}
                            size={20}
                            color={colors.textTertiary}
                        />
                    </TouchableOpacity>
                )}
            </View>
            {error && <Text style={styles.errorText}>{error}</Text>}
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        marginBottom: spacing.base,
    },
    label: {
        ...typography.label,
        color: colors.textPrimary,
        marginBottom: spacing.sm,
    },
    inputContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.surface,
        borderRadius: spacing.inputRadius,
        borderWidth: 1.5,
        borderColor: colors.border,
        paddingHorizontal: spacing.inputPaddingH,
        ...shadows.sm,
    },
    inputFocused: {
        borderColor: colors.primary,
        backgroundColor: colors.white,
    },
    inputError: {
        borderColor: colors.danger,
    },
    inputDisabled: {
        backgroundColor: colors.borderLight,
        opacity: 0.7,
    },
    leftIcon: {
        marginRight: spacing.sm,
    },
    input: {
        flex: 1,
        ...typography.body,
        color: colors.textPrimary,
        paddingVertical: spacing.inputPaddingV,
    },
    multilineInput: {
        textAlignVertical: 'top',
    },
    eyeIcon: {
        padding: spacing.xs,
    },
    errorText: {
        ...typography.caption,
        color: colors.danger,
        marginTop: spacing.xs,
    },
});

export default Input;
