/**
 * SignupScreen
 * Registration form
 */

import React, { useState, useCallback } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../hooks/useAuth';
import { validateForm, registerRules } from '../../utils/validators';

import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';

const SignupScreen = ({ navigation }) => {
    const { register, loading, error, dismissError } = useAuth();
    const [formData, setFormData] = useState({
        firstName: '',
        lastName: '',
        email: '',
        password: '',
        phoneNumber: '',
    });
    const [errors, setErrors] = useState({});

    const handleSignup = useCallback(async () => {
        dismissError();
        const validation = validateForm(formData, registerRules);
        if (!validation.isValid) {
            setErrors(validation.errors);
            return;
        }
        setErrors({});
        await register(formData);
    }, [formData, register, dismissError]);

    const updateField = (field) => (value) => {
        setFormData((prev) => ({ ...prev, [field]: value }));
        if (errors[field]) {
            setErrors((prev) => ({ ...prev, [field]: null }));
        }
    };

    return (
        <LinearGradient colors={colors.gradients.hero} style={styles.gradient}>
            <SafeAreaView style={styles.safeArea}>
                <KeyboardAvoidingView
                    behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
                    style={styles.container}
                >
                    <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={styles.scrollContent}>
                    {/* Header */}
                    <View style={styles.header}>
                        <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                            <Ionicons name="arrow-back" size={24} color={colors.white} />
                        </TouchableOpacity>
                        <Text style={styles.title}>Create Account</Text>
                        <Text style={styles.subtitle}>Join ParkEase today</Text>
                    </View>

                    {/* Form */}
                    <View style={styles.formCard}>
                        {error && (
                            <View style={styles.errorBanner}>
                                <Ionicons name="alert-circle" size={18} color={colors.danger} />
                                <Text style={styles.errorBannerText}>{error}</Text>
                            </View>
                        )}



                        <View style={styles.nameRow}>
                            <View style={styles.nameField}>
                                <Input label="First Name" value={formData.firstName} onChangeText={updateField('firstName')} placeholder="First" leftIcon="person-outline" error={errors.firstName} />
                            </View>
                            <View style={styles.nameField}>
                                <Input label="Last Name" value={formData.lastName} onChangeText={updateField('lastName')} placeholder="Last" error={errors.lastName} />
                            </View>
                        </View>

                        <Input label="Email" value={formData.email} onChangeText={updateField('email')} placeholder="Enter your email" keyboardType="email-address" autoCapitalize="none" leftIcon="mail-outline" error={errors.email} />

                        <Input label="Phone Number" value={formData.phoneNumber} onChangeText={updateField('phoneNumber')} placeholder="Enter phone number" keyboardType="phone-pad" leftIcon="call-outline" error={errors.phoneNumber} />

                        <Input label="Password" value={formData.password} onChangeText={updateField('password')} placeholder="Min. 8 characters" secureTextEntry leftIcon="lock-closed-outline" error={errors.password} />

                        <Button title="Create Account" onPress={handleSignup} loading={loading} style={styles.signupButton} />

                        <View style={styles.loginRow}>
                            <Text style={styles.loginText}>Already have an account? </Text>
                            <TouchableOpacity onPress={() => navigation.goBack()}>
                                <Text style={styles.loginLink}>Sign In</Text>
                            </TouchableOpacity>
                        </View>
                    </View>
                </ScrollView>
                </KeyboardAvoidingView>
            </SafeAreaView>
        </LinearGradient>
    );
};

const styles = StyleSheet.create({
    gradient: { flex: 1 },
    safeArea: { flex: 1 },
    container: { flex: 1 },
    scrollContent: { flexGrow: 1, paddingHorizontal: spacing.screenHorizontal, paddingTop: spacing.md, paddingBottom: 40 },
    header: { marginBottom: spacing.xl },
    backButton: { marginBottom: spacing.base },
    title: { fontSize: 32, fontWeight: '800', color: colors.white },
    subtitle: { ...typography.body, color: 'rgba(255,255,255,0.8)', marginTop: spacing.xs },
    formCard: {
        backgroundColor: colors.white,
        borderRadius: spacing.radius.xl,
        padding: spacing.xl,
        ...shadows.xl,
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
    errorBannerText: { ...typography.bodySmall, color: colors.dangerDark, flex: 1 },

    nameRow: { flexDirection: 'row', gap: spacing.md },
    nameField: { flex: 1 },
    signupButton: { marginTop: spacing.sm },
    loginRow: { flexDirection: 'row', justifyContent: 'center', marginTop: spacing.lg },
    loginText: { ...typography.bodySmall, color: colors.textSecondary },
    loginLink: { ...typography.bodySmall, color: colors.primary, fontWeight: typography.weight.semibold },
});

export default SignupScreen;
