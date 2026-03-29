/**
 * ProfileScreen
 * User profile, edit info, change password, logout
 */

import React, { useState, useCallback } from 'react';
import { View, Text, TouchableOpacity, Alert, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../hooks/useAuth';
import { authService } from '../../services/auth/authService';
import { deleteAccountThunk } from '../../store/slices/authSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';


const MenuItem = ({ icon, label, value, onPress, danger = false }) => (
    <TouchableOpacity style={menuStyles.item} onPress={onPress}>
        <Ionicons name={icon} size={22} color={danger ? colors.danger : colors.primary} />
        <View style={menuStyles.info}>
            <Text style={[menuStyles.label, danger && { color: colors.danger }]}>{label}</Text>
            {value && <Text style={menuStyles.value}>{value}</Text>}
        </View>
        <Ionicons name="chevron-forward" size={18} color={colors.textTertiary} />
    </TouchableOpacity>
);

const menuStyles = StyleSheet.create({
    item: { flexDirection: 'row', alignItems: 'center', paddingVertical: spacing.md, gap: spacing.md, borderBottomWidth: 1, borderBottomColor: colors.borderLight },
    info: { flex: 1 },
    label: { ...typography.body, color: colors.textPrimary },
    value: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
});

const ProfileScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { user, logout, updateProfile, loading } = useAuth();
    const [editing, setEditing] = useState(false);
    const [firstName, setFirstName] = useState(user?.firstName || '');
    const [lastName, setLastName] = useState(user?.lastName || '');
    const [phoneNumber, setPhoneNumber] = useState(user?.phoneNumber || '');

    // Get unread count safely
    const { notifications } = useSelector((s) => s.notification);
    const unreadCount = (Array.isArray(notifications) ? notifications : []).filter((n) => !n.isRead).length;

    const handleSaveProfile = useCallback(async () => {
        const result = await updateProfile({ firstName, lastName, phoneNumber });
        if (!result.error) {
            setEditing(false);
            Alert.alert('Success', 'Profile updated');
        }
    }, [updateProfile, firstName, lastName, phoneNumber]);

    const handleLogout = useCallback(() => {
        Alert.alert('Logout', 'Are you sure you want to logout?', [
            { text: 'Cancel', style: 'cancel' },
            { text: 'Logout', style: 'destructive', onPress: logout },
        ]);
    }, [logout]);

    const handleChangePassword = () => {
        navigation.navigate('ChangePassword');
    };

    const handleDeleteAccount = useCallback(() => {
        Alert.alert(
            'Delete Account',
            'This action is permanent and cannot be undone. Are you sure?',
            [
                { text: 'Cancel', style: 'cancel' },
                {
                    text: 'Delete',
                    style: 'destructive',
                    onPress: async () => {
                        try {
                            await dispatch(deleteAccountThunk()).unwrap();
                            Alert.alert('Account Deleted', 'Your account has been permanently deleted.');
                        } catch (error) {
                            Alert.alert('Error', error || 'Failed to delete account.');
                        }
                    },
                },
            ]
        );
    }, [dispatch]);

    return (
        <ScreenLayout scrollable>
            <View style={styles.content}>
                {/* Header */}
                <View style={styles.header}>
                    <Text style={styles.screenTitle}>Profile</Text>
                </View>

                {/* Avatar & Info */}
                <View style={styles.avatarSection}>
                    <View style={styles.avatarCircle}>
                        <Text style={styles.avatarText}>
                            {user?.firstName?.charAt(0)}{user?.lastName?.charAt(0)}
                        </Text>
                    </View>
                    <Text style={styles.userName}>{user?.firstName} {user?.lastName}</Text>
                    <Text style={styles.userEmail}>{user?.email}</Text>
                </View>

                {/* Edit Profile */}
                {editing ? (
                    <Card>
                        <Text style={styles.sectionTitle}>Edit Profile</Text>
                        <Input label="First Name" value={firstName} onChangeText={setFirstName} leftIcon="person-outline" />
                        <Input label="Last Name" value={lastName} onChangeText={setLastName} />
                        <Input label="Phone" value={phoneNumber} onChangeText={setPhoneNumber} keyboardType="phone-pad" leftIcon="call-outline" />
                        <View style={styles.editActions}>
                            <Button title="Save" onPress={handleSaveProfile} loading={loading} style={{ flex: 1 }} />
                            <Button title="Cancel" onPress={() => setEditing(false)} variant="ghost" style={{ flex: 1 }} />
                        </View>
                    </Card>
                ) : null}

                {/* Menu */}
                <Card>
                    <MenuItem icon="person-outline" label="Edit Profile" value={`${user?.firstName} ${user?.lastName}`} onPress={() => navigation.navigate('EditProfile')} />
                    <MenuItem icon="mail-outline" label="Email" value={user?.email} onPress={() => { }} />
                    <MenuItem icon="call-outline" label="Phone" value={user?.phoneNumber} onPress={() => { }} />
                    <MenuItem icon="lock-closed-outline" label="Change Password" onPress={handleChangePassword} />
                    <MenuItem icon="car-outline" label="My Vehicles" onPress={() => navigation.navigate('Vehicles', { addNew: true })} />
                    <MenuItem icon="heart-outline" label="Favorites" onPress={() => navigation.navigate('Favorites')} />
                    <MenuItem icon="notifications-outline" label="Notifications" value={unreadCount > 0 ? `${unreadCount} Unread` : ''} onPress={() => navigation.navigate('Notifications')} />
                </Card>

                <Card>
                    <MenuItem icon="trash-outline" label="Delete Account" onPress={handleDeleteAccount} danger />
                </Card>

                <Button title="Logout" onPress={handleLogout} variant="danger" style={styles.logoutBtn} icon={<Ionicons name="log-out-outline" size={20} color={colors.white} />} />
            </View>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    content: { paddingBottom: spacing['3xl'] },
    header: { paddingTop: spacing.md, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base },
    screenTitle: { ...typography.h2, color: colors.textPrimary },
    avatarSection: { alignItems: 'center', paddingVertical: spacing.xl },
    avatarCircle: { width: 80, height: 80, borderRadius: 40, backgroundColor: colors.primary, justifyContent: 'center', alignItems: 'center', ...shadows.lg },
    avatarText: { fontSize: 28, fontWeight: '700', color: colors.white },
    userName: { ...typography.h3, color: colors.textPrimary, marginTop: spacing.md },
    userEmail: { ...typography.bodySmall, color: colors.textSecondary, marginTop: 2 },
    roleBadge: { marginTop: spacing.sm, paddingHorizontal: spacing.md, paddingVertical: spacing.xs, backgroundColor: colors.primarySoft, borderRadius: spacing.radius.full },
    roleText: { ...typography.caption, color: colors.primary, fontWeight: '600' },
    sectionTitle: { ...typography.label, color: colors.textPrimary, marginBottom: spacing.md },
    editActions: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.sm },
    logoutBtn: { marginTop: spacing.xl, marginHorizontal: spacing.screenHorizontal },
});

export default ProfileScreen;
