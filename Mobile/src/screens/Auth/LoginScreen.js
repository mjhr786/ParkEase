/**
 * LoginScreen
 * Clean login matching mockup: blue gradient, rounded icon, white form card
 */

import React, { useState, useCallback, useEffect, useRef } from 'react';
import { EventBus } from '../../utils/EventBus';
import {
    View, Text, TouchableOpacity, StyleSheet, KeyboardAvoidingView,
    Platform, ScrollView, Alert, TextInput, Keyboard,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import * as Google from 'expo-auth-session/providers/google';
import * as WebBrowser from 'expo-web-browser';
import Constants from 'expo-constants';
import { useAuth } from '../../hooks/useAuth';
import { validateForm, loginRules } from '../../utils/validators';
import { colors, spacing, typography } from '../../styles/globalStyles';
import cacheService from '../../services/storage/cacheService';

WebBrowser.maybeCompleteAuthSession();

const GOOGLE_CLIENT_ID = Constants.expoConfig?.extra?.googleClientId
    || '1088567196242-9jpdbr13j8vlbf4o4fqjsqqumg07cd5g.apps.googleusercontent.com';

const LoginScreen = ({ navigation }) => {
    const { login, googleLogin, loading, error, dismissError } = useAuth();
    const [formData, setFormData] = useState({ email: '', password: '' });
    const [errors, setErrors] = useState({});
    const [rememberMe, setRememberMe] = useState(false);
    const [googleLoading, setGoogleLoading] = useState(false);
    const passwordRef = useRef(null);

    const [request, response, promptAsync] = Google.useAuthRequest({
        iosClientId: GOOGLE_CLIENT_ID,
        webClientId: GOOGLE_CLIENT_ID,
    });

    useEffect(() => {
        if (response?.type === 'success') {
            handleGoogleToken(response.authentication);
        } else if (response?.type === 'error') {
            Alert.alert('Google Sign-In Failed', response.error?.message || 'Something went wrong');
            setGoogleLoading(false);
        } else if (response?.type === 'dismiss') {
            setGoogleLoading(false);
        }
    }, [response]);

    const handleGoogleToken = async (authentication) => {
        try {
            setGoogleLoading(true);
            const userInfoResponse = await fetch('https://www.googleapis.com/userinfo/v2/me', {
                headers: { Authorization: `Bearer ${authentication.accessToken}` },
            });
            const userInfo = await userInfoResponse.json();
            if (googleLogin) {
                await googleLogin({
                    googleId: userInfo.id,
                    email: userInfo.email,
                    firstName: userInfo.given_name,
                    lastName: userInfo.family_name,
                    profilePicture: userInfo.picture,
                    idToken: authentication.idToken,
                    accessToken: authentication.accessToken,
                });
            }
        } catch (err) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Failed to process Google sign-in' });
        } finally {
            setGoogleLoading(false);
        }
    };

    useEffect(() => {
        (async () => {
            const savedEmail = await cacheService.getRememberEmail();
            if (savedEmail) {
                setFormData((prev) => ({ ...prev, email: savedEmail }));
                setRememberMe(true);
            }
        })();
    }, []);

    const handleLogin = useCallback(async () => {
        dismissError();
        const validation = validateForm(formData, loginRules);
        if (!validation.isValid) {
            setErrors(validation.errors);
            return;
        }
        setErrors({});
        Keyboard.dismiss();
        if (rememberMe) {
            await cacheService.setRememberEmail(formData.email);
        } else {
            await cacheService.clearRememberEmail();
        }
        await login(formData);
    }, [formData, login, dismissError, rememberMe]);

    const handleGoogleLogin = useCallback(async () => {
        setGoogleLoading(true);
        try {
            await promptAsync();
        } catch (err) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Could not open Google sign-in' });
            setGoogleLoading(false);
        }
    }, [promptAsync]);

    const updateField = (field) => (value) => {
        setFormData((prev) => ({ ...prev, [field]: value }));
        if (errors[field]) setErrors((prev) => ({ ...prev, [field]: null }));
    };

    return (
        <LinearGradient colors={['#1E40AF', '#2563EB', '#3B82F6']} style={styles.gradient}>
            <SafeAreaView style={styles.safeArea}>
                <KeyboardAvoidingView
                    behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
                    style={styles.container}
                >
                    <ScrollView
                        contentContainerStyle={styles.scrollContent}
                        showsVerticalScrollIndicator={false}
                        keyboardShouldPersistTaps="handled"
                    >
                        {/* Logo Area */}
                        <View style={styles.logoSection}>
                            <View style={styles.logoSquare}>
                                <Ionicons name="car-sport" size={40} color={colors.primary} />
                            </View>
                            <Text style={styles.appName}>ParkEasy</Text>
                            <Text style={styles.tagline}>
                                Find & Book Parking Spots Anytime, Anywhere.
                            </Text>
                        </View>

                        {/* Form Card */}
                        <View style={styles.formCard}>
                            <Text style={styles.welcomeText}>Welcome Back</Text>

                            {error && (
                                <View style={styles.errorBanner}>
                                    <Ionicons name="alert-circle" size={18} color={colors.danger} />
                                    <Text style={styles.errorBannerText}>{error}</Text>
                                </View>
                            )}

                            {/* Email */}
                            <Text style={styles.inputLabel}>Email</Text>
                            <View style={[styles.inputContainer, errors.email && styles.inputError]}>
                                <TextInput
                                    value={formData.email}
                                    onChangeText={updateField('email')}
                                    placeholder="you@example.com"
                                    placeholderTextColor={colors.textTertiary}
                                    style={styles.input}
                                    keyboardType="email-address"
                                    autoCapitalize="none"
                                    autoCorrect={false}
                                    textContentType="username"
                                    autoComplete="email"
                                    returnKeyType="next"
                                    onSubmitEditing={() => passwordRef.current?.focus()}
                                    blurOnSubmit={false}
                                />
                            </View>
                            {errors.email && <Text style={styles.errorText}>{errors.email}</Text>}

                            {/* Password */}
                            <Text style={styles.inputLabel}>Password</Text>
                            <View style={[styles.inputContainer, errors.password && styles.inputError]}>
                                <TextInput
                                    ref={passwordRef}
                                    value={formData.password}
                                    onChangeText={updateField('password')}
                                    placeholder="••••••••"
                                    placeholderTextColor={colors.textTertiary}
                                    style={styles.input}
                                    secureTextEntry
                                    textContentType="password"
                                    autoComplete="password"
                                    returnKeyType="done"
                                    onSubmitEditing={handleLogin}
                                />
                            </View>
                            {errors.password && <Text style={styles.errorText}>{errors.password}</Text>}

                            {/* Remember Me */}
                            <TouchableOpacity 
                                style={styles.rememberMeRow} 
                                onPress={() => setRememberMe(!rememberMe)}
                                activeOpacity={0.7}
                            >
                                <Ionicons 
                                    name={rememberMe ? "checkbox" : "square-outline"} 
                                    size={20} 
                                    color={rememberMe ? colors.primary : colors.textTertiary} 
                                />
                                <Text style={styles.rememberMeText}>Remember Me</Text>
                            </TouchableOpacity>

                            {/* Login Button */}
                            <TouchableOpacity
                                style={[styles.loginBtn, loading && styles.loginBtnDisabled]}
                                onPress={handleLogin}
                                disabled={loading || googleLoading}
                                activeOpacity={0.85}
                            >
                                <Text style={styles.loginBtnText}>
                                    {loading ? 'Signing in...' : 'Login'}
                                </Text>
                            </TouchableOpacity>

                            {/* Divider */}
                            <View style={styles.dividerRow}>
                                <View style={styles.dividerLine} />
                                <Text style={styles.dividerText}>OR</Text>
                                <View style={styles.dividerLine} />
                            </View>

                            {/* Google Login */}
                            <TouchableOpacity
                                style={styles.googleBtn}
                                onPress={handleGoogleLogin}
                                disabled={loading || googleLoading}
                                activeOpacity={0.8}
                            >
                                <View style={styles.googleIconWrap}>
                                    <Text style={styles.googleG}>G</Text>
                                </View>
                                <Text style={styles.googleBtnText}>
                                    {googleLoading ? 'Connecting...' : 'Continue with Google'}
                                </Text>
                            </TouchableOpacity>

                            {/* Sign Up Link */}
                            <View style={styles.signupRow}>
                                <Text style={styles.signupText}>Don't have an account? </Text>
                                <TouchableOpacity onPress={() => navigation.navigate('Signup')}>
                                    <Text style={styles.signupLink}>Sign up</Text>
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
    scrollContent: {
        flexGrow: 1,
        justifyContent: 'center',
        paddingHorizontal: 24,
    },
    // Logo
    logoSection: {
        alignItems: 'center',
        marginBottom: 32,
    },
    logoSquare: {
        width: 80,
        height: 80,
        borderRadius: 20,
        backgroundColor: colors.white,
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: 16,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.2,
        shadowRadius: 12,
        elevation: 8,
    },
    appName: {
        fontSize: 32,
        fontWeight: '800',
        color: colors.white,
        letterSpacing: 0.5,
    },
    tagline: {
        fontSize: 14,
        color: 'rgba(255,255,255,0.75)',
        marginTop: 6,
        textAlign: 'center',
    },
    // Form Card
    formCard: {
        backgroundColor: colors.white,
        borderRadius: 28,
        padding: 28,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 8 },
        shadowOpacity: 0.12,
        shadowRadius: 24,
        elevation: 12,
    },
    welcomeText: {
        fontSize: 24,
        fontWeight: '700',
        color: colors.textPrimary,
        marginBottom: 24,
        textAlign: 'center',
    },
    errorBanner: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.dangerSoft,
        padding: 12,
        borderRadius: 12,
        marginBottom: 16,
        gap: 8,
    },
    errorBannerText: {
        fontSize: 13,
        color: colors.dangerDark,
        flex: 1,
    },
    // Inputs
    inputLabel: {
        fontSize: 14,
        fontWeight: '600',
        color: colors.textPrimary,
        marginBottom: 8,
        marginTop: 4,
    },
    inputContainer: {
        backgroundColor: colors.background,
        borderRadius: 14,
        borderWidth: 1,
        borderColor: colors.border,
        marginBottom: 16,
    },
    inputError: {
        borderColor: colors.danger,
    },
    input: {
        fontSize: 15,
        color: colors.textPrimary,
        paddingHorizontal: 16,
        paddingVertical: 14,
    },
    errorText: {
        fontSize: 12,
        color: colors.danger,
        marginTop: -12,
        marginBottom: 12,
        marginLeft: 4,
    },
    // Remember Me
    rememberMeRow: {
        flexDirection: 'row',
        alignItems: 'center',
        marginBottom: 16,
        gap: 8,
    },
    rememberMeText: {
        fontSize: 14,
        color: colors.textSecondary,
        fontWeight: '500',
    },
    // Login Button
    loginBtn: {
        backgroundColor: colors.primary,
        borderRadius: 14,
        paddingVertical: 16,
        alignItems: 'center',
        marginTop: 8,
    },
    loginBtnDisabled: {
        opacity: 0.7,
    },
    loginBtnText: {
        fontSize: 16,
        fontWeight: '700',
        color: colors.white,
    },
    // Divider
    dividerRow: {
        flexDirection: 'row',
        alignItems: 'center',
        marginVertical: 20,
    },
    dividerLine: {
        flex: 1,
        height: 1,
        backgroundColor: colors.border,
    },
    dividerText: {
        fontSize: 12,
        color: colors.textTertiary,
        marginHorizontal: 16,
        fontWeight: '500',
    },
    // Google Button
    googleBtn: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: colors.white,
        borderWidth: 1.5,
        borderColor: colors.border,
        borderRadius: 14,
        paddingVertical: 14,
        gap: 10,
    },
    googleIconWrap: {
        width: 22,
        height: 22,
        borderRadius: 11,
        justifyContent: 'center',
        alignItems: 'center',
    },
    googleG: {
        fontSize: 18,
        fontWeight: '700',
        color: '#4285F4',
    },
    googleBtnText: {
        fontSize: 15,
        color: colors.textPrimary,
        fontWeight: '600',
    },
    // Sign Up
    signupRow: {
        flexDirection: 'row',
        justifyContent: 'center',
        marginTop: 20,
    },
    signupText: {
        fontSize: 14,
        color: colors.textSecondary,
    },
    signupLink: {
        fontSize: 14,
        color: colors.primary,
        fontWeight: '600',
    },
});

export default LoginScreen;
