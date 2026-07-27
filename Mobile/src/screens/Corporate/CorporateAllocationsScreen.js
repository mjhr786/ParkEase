import React, { useEffect, useState, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, Modal, ScrollView } from 'react-native';
import { useSelector } from 'react-redux';
import corporateService from '../../services/api/corporateService';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { globalStyles, colors, spacing, typography } from '../../styles/globalStyles';
import { EventBus } from '../../utils/EventBus';
import { Ionicons } from '@expo/vector-icons';

const CorporateAllocationsScreen = () => {
    const { activeCompanyId } = useSelector((state) => state.corporate);
    
    const [allocations, setAllocations] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    
    // Request Modal
    const [isRequestModalVisible, setRequestModalVisible] = useState(false);
    const [isRequesting, setIsRequesting] = useState(false);
    
    // Form data (simplified for this demo UI)
    const [parkingSpaceId, setParkingSpaceId] = useState('');
    const [totalSlots, setTotalSlots] = useState('10');
    const [monthlyRate, setMonthlyRate] = useState('50000');

    const loadAllocations = useCallback(async () => {
        if (!activeCompanyId) return;
        setIsLoading(true);
        try {
            const data = await corporateService.getAllocations(activeCompanyId);
            setAllocations(data || []);
        } catch (error) {
            console.error('Failed to load allocations', error);
        } finally {
            setIsLoading(false);
        }
    }, [activeCompanyId]);

    useEffect(() => {
        loadAllocations();
    }, [loadAllocations]);

    const handleRequestAllocation = async () => {
        if (!parkingSpaceId) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Parking Space ID is required' });
            return;
        }

        setIsRequesting(true);
        try {
            await corporateService.requestAllocation(activeCompanyId, {
                parkingSpaceId,
                totalSlots: parseInt(totalSlots, 10),
                fixedSlots: 0,
                sharedSlots: parseInt(totalSlots, 10),
                monthlyRate: parseFloat(monthlyRate),
                startDate: new Date().toISOString().split('T')[0],
                endDate: new Date(new Date().setFullYear(new Date().getFullYear() + 1)).toISOString().split('T')[0],
                leaseReference: `L-${Date.now()}`
            });
            EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Allocation requested!', type: 'success' });
            setRequestModalVisible(false);
            loadAllocations();
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error.response?.data?.message || 'Failed to request allocation' });
        } finally {
            setIsRequesting(false);
        }
    };

    const renderAllocationItem = ({ item }) => (
        <Card>
            <View style={globalStyles.rowBetween}>
                <View>
                    <Text style={typography.h3}>Lease Ref: {item.leaseReference}</Text>
                    <Text style={typography.bodySmall}>Slots: {item.totalSlots} ({item.fixedSlots} fixed, {item.sharedSlots} shared)</Text>
                    <Text style={typography.caption}>Status: {item.status === 1 ? 'Active' : 'Pending'}</Text>
                </View>
                <View style={styles.badge}>
                    <Text style={styles.badgeText}>{item.status === 1 ? 'Active' : 'Pending'}</Text>
                </View>
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
                    <Text style={globalStyles.sectionTitle}>Allocations & Leases</Text>
                    <Button 
                        title="Request" 
                        size="small" 
                        onPress={() => setRequestModalVisible(true)} 
                    />
                </View>

                <FlatList
                    data={allocations}
                    keyExtractor={(item) => item.id}
                    renderItem={renderAllocationItem}
                    contentContainerStyle={styles.listContent}
                    ListEmptyComponent={
                        <View style={globalStyles.center}>
                            <Text style={typography.body}>No active leases or allocations.</Text>
                        </View>
                    }
                    refreshing={isLoading}
                    onRefresh={loadAllocations}
                />
            </View>

            {/* Request Modal */}
            <Modal visible={isRequestModalVisible} animationType="slide" presentationStyle="pageSheet">
                <View style={styles.modalContainer}>
                    <View style={styles.modalHeader}>
                        <Text style={typography.h2}>Request Allocation</Text>
                        <TouchableOpacity onPress={() => setRequestModalVisible(false)}>
                            <Ionicons name="close" size={28} color={colors.textPrimary} />
                        </TouchableOpacity>
                    </View>
                    
                    <ScrollView contentContainerStyle={styles.modalBody}>
                        <Text style={[typography.caption, { marginBottom: spacing.md }]}>
                            Enter the ID of the parking space you wish to lease in bulk.
                        </Text>
                        <Input 
                            label="Parking Space ID" 
                            value={parkingSpaceId} 
                            onChangeText={setParkingSpaceId} 
                            placeholder="Enter GUID" 
                        />
                        <Input 
                            label="Total Slots Needed" 
                            value={totalSlots} 
                            onChangeText={setTotalSlots} 
                            keyboardType="number-pad" 
                        />
                        <Input 
                            label="Proposed Monthly Rate (₹)" 
                            value={monthlyRate} 
                            onChangeText={setMonthlyRate} 
                            keyboardType="number-pad" 
                        />
                        
                        <Button 
                            title="Submit Request" 
                            onPress={handleRequestAllocation} 
                            loading={isRequesting}
                            style={{ marginTop: spacing.md }}
                        />
                    </ScrollView>
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
    badge: {
        backgroundColor: colors.primarySoft,
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: spacing.radius.full,
    },
    badgeText: {
        ...typography.caption,
        color: colors.primary,
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
        paddingTop: spacing.md,
    }
});

export default CorporateAllocationsScreen;
