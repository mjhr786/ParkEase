import React, { useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, SafeAreaView, Alert, KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../../styles/globalStyles';
import Input from '../../components/Common/Input';
import Button from '../../components/Common/Button';
import authService from '../../services/auth/authService';

const ChangePasswordScreen = ({ navigation }) => {
    const [currentPassword, setCurrentPassword] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSave = async () => {
        if (!currentPassword || !newPassword || !confirmPassword) {
            Alert.alert('Error', 'Please fill in all fields');
            return;
        }

        if (newPassword !== confirmPassword) {
            Alert.alert('Error', 'New passwords do not match');
            return;
        }

        if (newPassword.length < 8) {
            Alert.alert('Error', 'New password must be at least 8 characters long');
            return;
        }

        try {
            setLoading(true);
            const response = await authService.changePassword({
                currentPassword,
                newPassword,
            });

            if (response.success) {
                Alert.alert('Success', 'Your password has been updated successfully', [
                    { text: 'OK', onPress: () => navigation.goBack() }
                ]);
            } else {
                Alert.alert('Error', response.message || 'Failed to update password');
            }
        } catch (error) {
            Alert.alert('Error', error.response?.data?.message || 'Failed to update password');
        } finally {
            setLoading(false);
        }
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Change Password</Text>
                <View style={{ width: 34 }} />
            </View>

            <KeyboardAvoidingView 
                style={styles.keyboardView}
                behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
            >
                <ScrollView contentContainerStyle={styles.scrollContent}>
                    <Text style={styles.description}>
                        Your password must be at least 8 characters and should include a combination of numbers, letters, and special characters.
                    </Text>

                    <Input
                        label="Current Password"
                        placeholder="Enter current password"
                        value={currentPassword}
                        onChangeText={setCurrentPassword}
                        secureTextEntry
                        icon="lock-closed-outline"
                    />

                    <View style={styles.divider} />

                    <Input
                        label="New Password"
                        placeholder="Enter new password"
                        value={newPassword}
                        onChangeText={setNewPassword}
                        secureTextEntry
                        icon="lock-closed-outline"
                    />

                    <Input
                        label="Confirm New Password"
                        placeholder="Re-enter new password"
                        value={confirmPassword}
                        onChangeText={setConfirmPassword}
                        secureTextEntry
                        icon="lock-closed-outline"
                    />

                    <Button
                        title="Update Password"
                        onPress={handleSave}
                        loading={loading}
                        style={styles.submitButton}
                    />
                </ScrollView>
            </KeyboardAvoidingView>
        </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.background,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingHorizontal: 20,
        paddingVertical: 15,
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    backButton: {
        padding: 5,
    },
    headerTitle: {
        ...typography.h3,
        color: colors.text,
    },
    keyboardView: {
        flex: 1,
    },
    scrollContent: {
        padding: 20,
    },
    description: {
        ...typography.body2,
        color: colors.textSecondary,
        marginBottom: 25,
        lineHeight: 20,
    },
    divider: {
        height: 1,
        backgroundColor: colors.borderLight,
        marginVertical: 20,
    },
    submitButton: {
        marginTop: 30,
    },
});

export default ChangePasswordScreen;
