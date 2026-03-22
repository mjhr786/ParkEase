/**
 * ChangePasswordScreen
 * Dedicated screen for changing password with current, new, and confirm fields
 */

import React, { useState, useCallback } from 'react';
import { View, Text, StyleSheet, Alert } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { changePasswordThunk } from '../../store/slices/authSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography } from '../../styles/globalStyles';

const ChangePasswordScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { loading } = useSelector((state) => state.auth);
    const [currentPassword, setCurrentPassword] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');

    const handleSubmit = useCallback(async () => {
        if (!currentPassword.trim()) {
            Alert.alert('Error', 'Please enter your current password.');
            return;
        }
        if (newPassword.length < 8) {
            Alert.alert('Error', 'New password must be at least 8 characters.');
            return;
        }
        if (newPassword !== confirmPassword) {
            Alert.alert('Error', 'New passwords do not match.');
            return;
        }

        const result = await dispatch(changePasswordThunk({
            currentPassword,
            newPassword,
        }));

        if (!result.error) {
            Alert.alert('Success', 'Your password has been changed.', [
                { text: 'OK', onPress: () => navigation.goBack() },
            ]);
        } else {
            Alert.alert('Error', result.payload || 'Failed to change password.');
        }
    }, [dispatch, currentPassword, newPassword, confirmPassword, navigation]);

    return (
        <ScreenLayout scrollable>
            <View style={styles.header}>
                <Ionicons name="arrow-back" size={24} color={colors.textPrimary} onPress={() => navigation.goBack()} />
                <Text style={styles.title}>Change Password</Text>
                <View style={{ width: 24 }} />
            </View>

            <Card>
                <Input
                    label="Current Password"
                    value={currentPassword}
                    onChangeText={setCurrentPassword}
                    secureTextEntry
                    leftIcon="lock-closed-outline"
                    placeholder="Enter current password"
                />
                <Input
                    label="New Password"
                    value={newPassword}
                    onChangeText={setNewPassword}
                    secureTextEntry
                    leftIcon="key-outline"
                    placeholder="Enter new password (min 8 chars)"
                />
                <Input
                    label="Confirm New Password"
                    value={confirmPassword}
                    onChangeText={setConfirmPassword}
                    secureTextEntry
                    leftIcon="key-outline"
                    placeholder="Confirm new password"
                />
                <Button
                    title="Change Password"
                    onPress={handleSubmit}
                    loading={loading}
                    style={{ marginTop: spacing.lg }}
                    icon={<Ionicons name="shield-checkmark-outline" size={18} color={colors.white} />}
                />
            </Card>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: 60,
        paddingBottom: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
    },
    title: { ...typography.h3, color: colors.textPrimary },
});

export default ChangePasswordScreen;
