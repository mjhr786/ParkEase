/**
 * LoginScreen
 * Email + password login with premium gradient background
 */

import React, { useState, useCallback } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, KeyboardAvoidingView, Platform, Image } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../hooks/useAuth';
import { validateForm, loginRules } from '../../utils/validators';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography } from '../../styles/globalStyles';

const LoginScreen = ({ navigation }) => {
    const { login, loading, error, dismissError } = useAuth();
    const [formData, setFormData] = useState({ email: '', password: '' });
    const [errors, setErrors] = useState({});

    const handleLogin = useCallback(async () => {
        dismissError();
        const validation = validateForm(formData, loginRules);
        if (!validation.isValid) {
            setErrors(validation.errors);
            return;
        }
        setErrors({});
        const result = await login(formData);
        if (result.error) {
            // Error handled via redux state
        }
    }, [formData, login, dismissError]);

    const updateField = (field) => (value) => {
        setFormData((prev) => ({ ...prev, [field]: value }));
        if (errors[field]) {
            setErrors((prev) => ({ ...prev, [field]: null }));
        }
    };

    return (
        <LinearGradient colors={colors.gradients.hero} style={styles.gradient}>
            <KeyboardAvoidingView
                behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
                style={styles.container}
            >
                {/* Logo Area */}
                <View style={styles.logoSection}>
                    <View style={styles.logoCircle}>
                        <Ionicons name="car-sport" size={48} color={colors.primary} />
                    </View>
                    <Text style={styles.appName}>ParkEase</Text>
                    <Text style={styles.tagline}>Find & book parking in seconds</Text>
                </View>

                {/* Form */}
                <View style={styles.formCard}>
                    <Text style={styles.welcomeText}>Welcome Back</Text>

                    {error && (
                        <View style={styles.errorBanner}>
                            <Ionicons name="alert-circle" size={18} color={colors.danger} />
                            <Text style={styles.errorBannerText}>{error}</Text>
                        </View>
                    )}

                    <Input
                        label="Email"
                        value={formData.email}
                        onChangeText={updateField('email')}
                        placeholder="Enter your email"
                        keyboardType="email-address"
                        autoCapitalize="none"
                        leftIcon="mail-outline"
                        error={errors.email}
                    />

                    <Input
                        label="Password"
                        value={formData.password}
                        onChangeText={updateField('password')}
                        placeholder="Enter your password"
                        secureTextEntry
                        leftIcon="lock-closed-outline"
                        error={errors.password}
                    />

                    <Button
                        title="Sign In"
                        onPress={handleLogin}
                        loading={loading}
                        style={styles.loginButton}
                    />

                    <View style={styles.signupRow}>
                        <Text style={styles.signupText}>Don't have an account? </Text>
                        <TouchableOpacity onPress={() => navigation.navigate('Signup')}>
                            <Text style={styles.signupLink}>Sign Up</Text>
                        </TouchableOpacity>
                    </View>
                </View>
            </KeyboardAvoidingView>
        </LinearGradient>
    );
};

const styles = StyleSheet.create({
    gradient: {
        flex: 1,
    },
    container: {
        flex: 1,
        justifyContent: 'center',
        paddingHorizontal: spacing.screenHorizontal,
    },
    logoSection: {
        alignItems: 'center',
        marginBottom: spacing['2xl'],
    },
    logoCircle: {
        width: 88,
        height: 88,
        borderRadius: 44,
        backgroundColor: colors.white,
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: spacing.base,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.2,
        shadowRadius: 12,
        elevation: 8,
    },
    appName: {
        fontSize: 36,
        fontWeight: '800',
        color: colors.white,
        letterSpacing: 1,
    },
    tagline: {
        ...typography.bodySmall,
        color: 'rgba(255,255,255,0.8)',
        marginTop: spacing.xs,
    },
    formCard: {
        backgroundColor: colors.white,
        borderRadius: spacing.radius.xl,
        padding: spacing.xl,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 8 },
        shadowOpacity: 0.15,
        shadowRadius: 24,
        elevation: 12,
    },
    welcomeText: {
        ...typography.h2,
        color: colors.textPrimary,
        marginBottom: spacing.lg,
        textAlign: 'center',
    },
    errorBanner: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.dangerSoft,
        padding: spacing.md,
        borderRadius: spacing.radius.md,
        marginBottom: spacing.base,
        gap: spacing.sm,
    },
    errorBannerText: {
        ...typography.bodySmall,
        color: colors.dangerDark,
        flex: 1,
    },
    loginButton: {
        marginTop: spacing.sm,
    },
    signupRow: {
        flexDirection: 'row',
        justifyContent: 'center',
        marginTop: spacing.lg,
    },
    signupText: {
        ...typography.bodySmall,
        color: colors.textSecondary,
    },
    signupLink: {
        ...typography.bodySmall,
        color: colors.primary,
        fontWeight: typography.weight.semibold,
    },
});

export default LoginScreen;
