import React, { useEffect, useState, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, Modal, Alert } from 'react-native';
import { useSelector } from 'react-redux';
import corporateService from '../../services/api/corporateService';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { globalStyles, colors, spacing, typography } from '../../styles/globalStyles';
import { EventBus } from '../../utils/EventBus';
import { Ionicons } from '@expo/vector-icons';

const CorporateMembersScreen = () => {
    const { activeCompanyId } = useSelector((state) => state.corporate);
    
    const [members, setMembers] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    
    // Invite Modal
    const [isInviteModalVisible, setInviteModalVisible] = useState(false);
    const [inviteEmail, setInviteEmail] = useState('');
    const [isInviting, setIsInviting] = useState(false);

    const loadMembers = useCallback(async () => {
        if (!activeCompanyId) return;
        setIsLoading(true);
        try {
            const data = await corporateService.getMembers(activeCompanyId, { page: 1, pageSize: 50 });
            // Assuming data is ApiResponse<PaginatedList<MemberDto>> 
            setMembers(data?.items || data || []);
        } catch (error) {
            console.error('Failed to load members', error);
        } finally {
            setIsLoading(false);
        }
    }, [activeCompanyId]);

    useEffect(() => {
        loadMembers();
    }, [loadMembers]);

    const handleInvite = async () => {
        if (!inviteEmail) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Email is required' });
            return;
        }

        setIsInviting(true);
        try {
            await corporateService.inviteMember(activeCompanyId, { email: inviteEmail, role: 0 }); // 0 = Employee
            EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Invitation sent!', type: 'success' });
            setInviteModalVisible(false);
            setInviteEmail('');
            loadMembers();
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error.response?.data?.message || 'Failed to send invitation' });
        } finally {
            setIsInviting(false);
        }
    };

    const handleRemoveMember = (membershipId) => {
        Alert.alert('Remove Member', 'Are you sure you want to remove this member?', [
            { text: 'Cancel', style: 'cancel' },
            { 
                text: 'Remove', 
                style: 'destructive',
                onPress: async () => {
                    try {
                        await corporateService.removeMember(activeCompanyId, membershipId);
                        EventBus.emit('SHOW_BANNER', { title: 'Removed', message: 'Member removed successfully', type: 'success' });
                        loadMembers();
                    } catch (error) {
                        EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Failed to remove member' });
                    }
                }
            }
        ]);
    };

    const renderMemberItem = ({ item }) => (
        <Card style={styles.memberCard}>
            <View style={globalStyles.rowBetween}>
                <View style={globalStyles.row}>
                    <View style={styles.avatar}>
                        <Ionicons name="person" size={20} color={colors.white} />
                    </View>
                    <View style={{ marginLeft: spacing.md }}>
                        <Text style={styles.memberName}>{item.user?.firstName} {item.user?.lastName}</Text>
                        <Text style={styles.memberEmail}>{item.user?.email || item.email}</Text>
                        <View style={styles.roleBadge}>
                            <Text style={styles.roleText}>{item.role === 1 ? 'Admin' : 'Employee'}</Text>
                        </View>
                    </View>
                </View>
                <TouchableOpacity onPress={() => handleRemoveMember(item.id)} style={styles.deleteBtn}>
                    <Ionicons name="trash-outline" size={20} color={colors.danger} />
                </TouchableOpacity>
            </View>
        </Card>
    );

    if (!activeCompanyId) {
        return (
            <ScreenLayout edges={['top']}>
                <View style={globalStyles.center}>
                    <Text style={typography.body}>Please set an Active Company first.</Text>
                </View>
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout scrollable={false} edges={['top']}>
            <View style={globalStyles.screenPadded}>
                <View style={styles.header}>
                    <Text style={globalStyles.sectionTitle}>Members</Text>
                    <Button 
                        title="Invite" 
                        size="small" 
                        onPress={() => setInviteModalVisible(true)} 
                    />
                </View>

                <FlatList
                    data={members}
                    keyExtractor={(item) => item.id}
                    renderItem={renderMemberItem}
                    contentContainerStyle={styles.listContent}
                    ListEmptyComponent={
                        <View style={globalStyles.center}>
                            <Text style={typography.body}>No members found.</Text>
                        </View>
                    }
                    refreshing={isLoading}
                    onRefresh={loadMembers}
                />
            </View>

            {/* Invite Modal */}
            <Modal visible={isInviteModalVisible} animationType="slide" presentationStyle="pageSheet">
                <View style={styles.modalContainer}>
                    <View style={styles.modalHeader}>
                        <Text style={typography.h2}>Invite Member</Text>
                        <TouchableOpacity onPress={() => setInviteModalVisible(false)}>
                            <Ionicons name="close" size={28} color={colors.textPrimary} />
                        </TouchableOpacity>
                    </View>
                    
                    <View style={styles.modalBody}>
                        <Input 
                            label="Email Address" 
                            value={inviteEmail} 
                            onChangeText={setInviteEmail} 
                            keyboardType="email-address" 
                            placeholder="employee@company.com" 
                        />
                        
                        <Button 
                            title="Send Invitation" 
                            onPress={handleInvite} 
                            loading={isInviting}
                            style={{ marginTop: spacing.md }}
                        />
                    </View>
                </View>
            </Modal>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingVertical: spacing.md,
    },
    listContent: {
        paddingBottom: spacing.xl,
    },
    memberCard: {
        padding: spacing.md,
    },
    avatar: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
    },
    memberName: {
        ...typography.body,
        fontWeight: 'bold',
        color: colors.textPrimary,
    },
    memberEmail: {
        ...typography.caption,
        color: colors.textSecondary,
    },
    roleBadge: {
        backgroundColor: colors.primary + '20',
        paddingHorizontal: 8,
        paddingVertical: 2,
        borderRadius: 4,
        marginTop: 4,
        alignSelf: 'flex-start',
    },
    roleText: {
        fontSize: 10,
        color: colors.primary,
        fontWeight: '600',
    },
    deleteBtn: {
        padding: spacing.sm,
    },
    modalContainer: {
        flex: 1,
        backgroundColor: colors.background,
    },
    modalHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: spacing.screenHorizontal,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    modalBody: {
        padding: spacing.screenHorizontal,
    }
});

export default CorporateMembersScreen;
