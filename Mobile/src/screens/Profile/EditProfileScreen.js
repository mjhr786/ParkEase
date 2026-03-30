/**
 * EditProfileScreen
 * Dedicated screen for updating user details
 */

import React, { useState, useCallback } from 'react';
import { EventBus } from '../../utils/EventBus';
import { View, Text, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { updateProfileThunk } from '../../store/slices/authSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography } from '../../styles/globalStyles';

const EditProfileScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user, loading } = useSelector((state) => state.auth);
    
    const [firstName, setFirstName] = useState(user?.firstName || '');
    const [lastName, setLastName] = useState(user?.lastName || '');
    const [phoneNumber, setPhoneNumber] = useState(user?.phoneNumber || '');

    const handleSubmit = useCallback(async () => {
        if (!firstName.trim() || !lastName.trim()) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'First name and last name are required.' });
            return;
        }

        const result = await dispatch(updateProfileThunk({
            firstName: firstName.trim(),
            lastName: lastName.trim(),
            phoneNumber: phoneNumber.trim(),
        }));

        if (!result.error) {
            EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Profile updated successfully.', type: 'success' });
            navigation.goBack();
        } else {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: result.payload || 'Failed to update profile.' });
        }
    }, [dispatch, firstName, lastName, phoneNumber, navigation]);

    return (
        <ScreenLayout scrollable>
            <View style={styles.header}>
                <Ionicons name="arrow-back" size={24} color={colors.textPrimary} onPress={() => navigation.goBack()} />
                <Text style={styles.title}>Edit Profile</Text>
                <View style={{ width: 24 }} />
            </View>

            <Card>
                <Input
                    label="First Name"
                    value={firstName}
                    onChangeText={setFirstName}
                    leftIcon="person-outline"
                    placeholder="Enter first name"
                />
                <Input
                    label="Last Name"
                    value={lastName}
                    onChangeText={setLastName}
                    leftIcon="person-outline"
                    placeholder="Enter last name"
                />
                <Input
                    label="Phone Number"
                    value={phoneNumber}
                    onChangeText={setPhoneNumber}
                    keyboardType="phone-pad"
                    leftIcon="call-outline"
                    placeholder="Enter phone number"
                />
                <Button
                    title="Save Changes"
                    onPress={handleSubmit}
                    loading={loading}
                    style={{ marginTop: spacing.lg }}
                    icon={<Ionicons name="save-outline" size={18} color={colors.white} />}
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
        paddingTop: spacing.md,
        paddingBottom: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
    },
    title: { ...typography.h3, color: colors.textPrimary },
});

export default EditProfileScreen;
