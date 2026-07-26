import React, { useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, SafeAreaView, Alert, KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../../styles/globalStyles';
import Input from '../../components/Common/Input';
import Button from '../../components/Common/Button';
import { updateProfileThunk } from '../../store/slices/authSlice';

const EditProfileScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user, loading } = useSelector(state => state.auth);

    const [firstName, setFirstName] = useState(user?.firstName || '');
    const [lastName, setLastName] = useState(user?.lastName || '');
    const [phoneNumber, setPhoneNumber] = useState(user?.phoneNumber || '');

    const handleSave = async () => {
        if (!firstName || !lastName) {
            Alert.alert('Error', 'First name and last name are required');
            return;
        }

        try {
            const resultAction = await dispatch(updateProfileThunk({
                firstName,
                lastName,
                phoneNumber
            }));

            if (updateProfileThunk.fulfilled.match(resultAction)) {
                Alert.alert('Success', 'Profile updated successfully', [
                    { text: 'OK', onPress: () => navigation.goBack() }
                ]);
            } else {
                Alert.alert('Error', resultAction.payload || 'Failed to update profile');
            }
        } catch (error) {
            Alert.alert('Error', 'An unexpected error occurred');
        }
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Edit Profile</Text>
                <View style={{ width: 34 }} />
            </View>

            <KeyboardAvoidingView 
                style={styles.keyboardView}
                behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
            >
                <ScrollView contentContainerStyle={styles.scrollContent}>
                    
                    <View style={styles.avatarSection}>
                        <View style={styles.avatarPlaceholder}>
                            <Text style={styles.avatarText}>
                                {firstName ? firstName.charAt(0).toUpperCase() : 'U'}
                            </Text>
                            <TouchableOpacity style={styles.editAvatarButton}>
                                <Ionicons name="camera" size={16} color="#FFF" />
                            </TouchableOpacity>
                        </View>
                        <Text style={styles.emailText}>{user?.email}</Text>
                    </View>

                    <Input
                        label="First Name"
                        placeholder="Enter your first name"
                        value={firstName}
                        onChangeText={setFirstName}
                        icon="person-outline"
                    />

                    <Input
                        label="Last Name"
                        placeholder="Enter your last name"
                        value={lastName}
                        onChangeText={setLastName}
                        icon="person-outline"
                    />

                    <Input
                        label="Phone Number"
                        placeholder="Enter your phone number"
                        value={phoneNumber}
                        onChangeText={setPhoneNumber}
                        icon="call-outline"
                        keyboardType="phone-pad"
                    />

                    <Button
                        title="Save Changes"
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
    avatarSection: {
        alignItems: 'center',
        marginBottom: 30,
        marginTop: 10,
    },
    avatarPlaceholder: {
        width: 100,
        height: 100,
        borderRadius: 50,
        backgroundColor: colors.primary + '20',
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: 10,
        position: 'relative',
    },
    avatarText: {
        ...typography.h1,
        color: colors.primary,
    },
    editAvatarButton: {
        position: 'absolute',
        bottom: 0,
        right: 0,
        backgroundColor: colors.primary,
        width: 32,
        height: 32,
        borderRadius: 16,
        justifyContent: 'center',
        alignItems: 'center',
        borderWidth: 3,
        borderColor: colors.surface,
    },
    emailText: {
        ...typography.body2,
        color: colors.textSecondary,
    },
    submitButton: {
        marginTop: 20,
    },
});

export default EditProfileScreen;
