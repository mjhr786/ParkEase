import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, Modal } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { fetchMyCompanies, setActiveCompany } from '../../store/slices/corporateSlice';
import corporateService from '../../services/api/corporateService';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { globalStyles, colors, spacing, typography } from '../../styles/globalStyles';
import { EventBus } from '../../utils/EventBus';
import { Ionicons } from '@expo/vector-icons';

const CompanyManagementScreen = () => {
    const dispatch = useDispatch();
    const { myCompanies, activeCompanyId, isLoading } = useSelector((state) => state.corporate);

    const [isCreateModalVisible, setCreateModalVisible] = useState(false);
    const [isCreating, setIsCreating] = useState(false);
    
    // Form fields
    const [name, setName] = useState('');
    const [regNumber, setRegNumber] = useState('');
    const [email, setEmail] = useState('');
    const [phone, setPhone] = useState('');
    const [billingAddress, setBillingAddress] = useState('');

    useEffect(() => {
        dispatch(fetchMyCompanies());
    }, [dispatch]);

    const handleCreateCompany = async () => {
        if (!name || !email || !regNumber || !billingAddress) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Name, Email, Registration Number, and Billing Address are required' });
            return;
        }

        setIsCreating(true);
        try {
            await corporateService.createCompany({
                name,
                registrationNumber: regNumber,
                contactEmail: email,
                contactPhone: phone,
                billingAddress,
                billingType: 0 // default
            });
            EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Company created successfully', type: 'success' });
            setCreateModalVisible(false);
            dispatch(fetchMyCompanies()); // Refresh list
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error.response?.data?.message || 'Failed to create company' });
        } finally {
            setIsCreating(false);
        }
    };

    const renderCompanyItem = ({ item }) => {
        const isActive = item.id === activeCompanyId;

        return (
            <Card>
                <View style={globalStyles.rowBetween}>
                    <View style={{ flex: 1 }}>
                        <Text style={styles.companyName}>{item.name}</Text>
                        <Text style={styles.companyInfo}>{item.contactEmail}</Text>
                        {item.registrationNumber ? (
                            <Text style={styles.companyInfo}>Reg: {item.registrationNumber}</Text>
                        ) : null}
                    </View>
                    <View style={styles.actionContainer}>
                        {isActive ? (
                            <View style={styles.activeBadge}>
                                <Text style={styles.activeText}>Active</Text>
                            </View>
                        ) : (
                            <Button 
                                title="Switch" 
                                variant="outline" 
                                size="small"
                                onPress={() => {
                                    dispatch(setActiveCompany(item.id));
                                    EventBus.emit('SHOW_BANNER', { title: 'Switched', message: `Now viewing ${item.name}`, type: 'success' });
                                }} 
                            />
                        )}
                    </View>
                </View>
            </Card>
        );
    };

    return (
        <ScreenLayout scrollable={false} edges={['top']}>
            <View style={globalStyles.screenPadded}>
                <View style={styles.header}>
                    <Text style={globalStyles.sectionTitle}>My Companies</Text>
                    <Button 
                        title="Add New" 
                        size="small" 
                        onPress={() => setCreateModalVisible(true)} 
                    />
                </View>

                <FlatList
                    data={myCompanies}
                    keyExtractor={(item) => item.id}
                    renderItem={renderCompanyItem}
                    contentContainerStyle={styles.listContent}
                    ListEmptyComponent={
                        <View style={globalStyles.center}>
                            <Text style={typography.body}>You don't belong to any companies yet.</Text>
                        </View>
                    }
                    refreshing={isLoading}
                    onRefresh={() => dispatch(fetchMyCompanies())}
                />
            </View>

            <Modal visible={isCreateModalVisible} animationType="slide" presentationStyle="pageSheet">
                <View style={styles.modalContainer}>
                    <View style={styles.modalHeader}>
                        <Text style={typography.h2}>Create Company</Text>
                        <TouchableOpacity onPress={() => setCreateModalVisible(false)}>
                            <Ionicons name="close" size={28} color={colors.textPrimary} />
                        </TouchableOpacity>
                    </View>
                    
                    <View style={styles.modalBody}>
                        <Input label="Company Name" value={name} onChangeText={setName} placeholder="Acme Corp" />
                        <Input label="Contact Email" value={email} onChangeText={setEmail} keyboardType="email-address" />
                        <Input label="Contact Phone" value={phone} onChangeText={setPhone} keyboardType="phone-pad" />
                        <Input label="Registration Number" value={regNumber} onChangeText={setRegNumber} />
                        <Input label="Billing Address" value={billingAddress} onChangeText={setBillingAddress} />
                        
                        <Button 
                            title="Create Company" 
                            onPress={handleCreateCompany} 
                            loading={isCreating}
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
    companyName: {
        ...typography.h3,
        color: colors.textPrimary,
        marginBottom: 4,
    },
    companyInfo: {
        ...typography.caption,
        color: colors.textSecondary,
    },
    actionContainer: {
        justifyContent: 'center',
        alignItems: 'flex-end',
        marginLeft: spacing.md,
    },
    activeBadge: {
        backgroundColor: colors.success + '20',
        paddingHorizontal: spacing.sm,
        paddingVertical: 4,
        borderRadius: 12,
    },
    activeText: {
        ...typography.caption,
        color: colors.success,
        fontWeight: 'bold',
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

export default CompanyManagementScreen;
