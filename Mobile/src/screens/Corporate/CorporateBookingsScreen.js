import React, { useEffect, useState, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, Modal, ScrollView, Alert } from 'react-native';
import { useSelector } from 'react-redux';
import corporateService from '../../services/api/corporateService';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { globalStyles, colors, spacing, typography } from '../../styles/globalStyles';
import { EventBus } from '../../utils/EventBus';
import { Ionicons } from '@expo/vector-icons';

const CorporateBookingsScreen = () => {
    const { activeCompanyId } = useSelector((state) => state.corporate);
    
    const [bookings, setBookings] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    
    // Booking Modal
    const [isBookingModalVisible, setBookingModalVisible] = useState(false);
    const [isBooking, setIsBooking] = useState(false);
    
    // Form data
    const [allocationId, setAllocationId] = useState('');
    const [vehicleNumber, setVehicleNumber] = useState('');

    const loadBookings = useCallback(async () => {
        if (!activeCompanyId) return;
        setIsLoading(true);
        try {
            const data = await corporateService.getCorporateBookings(activeCompanyId, { page: 1, pageSize: 50 });
            setBookings(data?.items || data || []);
        } catch (error) {
            console.warn('Failed to load corporate bookings', error.message);
        } finally {
            setIsLoading(false);
        }
    }, [activeCompanyId]);

    useEffect(() => {
        loadBookings();
    }, [loadBookings]);

    const handleEmployeeBooking = async () => {
        if (!allocationId || !vehicleNumber) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Allocation ID and Vehicle Number required' });
            return;
        }

        setIsBooking(true);
        try {
            // Hardcoding dates for demo UI purposes
            const start = new Date();
            start.setHours(9, 0, 0, 0); // Today 9 AM
            const end = new Date();
            end.setHours(18, 0, 0, 0); // Today 6 PM

            await corporateService.createEmployeeBooking(activeCompanyId, {
                allocationId,
                startDateTime: start.toISOString(),
                endDateTime: end.toISOString(),
                vehicleType: 0,
                vehicleNumber
            });
            EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Booking confirmed!', type: 'success' });
            setBookingModalVisible(false);
            loadBookings();
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error.response?.data?.message || 'Failed to book slot' });
        } finally {
            setIsBooking(false);
        }
    };

    const handleCancel = (bookingId) => {
        Alert.alert('Cancel Booking', 'Are you sure you want to cancel this booking?', [
            { text: 'No', style: 'cancel' },
            { 
                text: 'Yes', 
                style: 'destructive',
                onPress: async () => {
                    try {
                        await corporateService.cancelCorporateBooking(activeCompanyId, bookingId, 'User cancelled');
                        EventBus.emit('SHOW_BANNER', { title: 'Cancelled', message: 'Booking cancelled successfully', type: 'success' });
                        loadBookings();
                    } catch (error) {
                        EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Failed to cancel' });
                    }
                }
            }
        ]);
    };

    const renderBookingItem = ({ item }) => {
        const startStr = new Date(item.startDateTime).toLocaleString();
        
        return (
            <Card style={styles.bookingCard}>
                <View style={globalStyles.rowBetween}>
                    <View>
                        <Text style={typography.h3}>Vehicle: {item.vehicleNumber || 'N/A'}</Text>
                        <Text style={typography.caption}>Start: {startStr}</Text>
                        <Text style={typography.caption}>Type: {item.isVisitor ? 'Visitor' : 'Employee'}</Text>
                    </View>
                    <TouchableOpacity onPress={() => handleCancel(item.id)} style={styles.cancelBtn}>
                        <Text style={styles.cancelText}>Cancel</Text>
                    </TouchableOpacity>
                </View>
            </Card>
        );
    };

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
                    <Text style={globalStyles.sectionTitle}>Bookings</Text>
                    <Button 
                        title="Book Slot" 
                        size="small" 
                        onPress={() => setBookingModalVisible(true)} 
                    />
                </View>

                <FlatList
                    data={bookings}
                    keyExtractor={(item) => item.id}
                    renderItem={renderBookingItem}
                    contentContainerStyle={styles.listContent}
                    ListEmptyComponent={
                        <View style={globalStyles.center}>
                            <Text style={typography.body}>No corporate bookings found.</Text>
                        </View>
                    }
                    refreshing={isLoading}
                    onRefresh={loadBookings}
                />
            </View>

            {/* Booking Modal */}
            <Modal visible={isBookingModalVisible} animationType="slide" presentationStyle="pageSheet">
                <View style={styles.modalContainer}>
                    <View style={styles.modalHeader}>
                        <Text style={typography.h2}>Book Employee Slot</Text>
                        <TouchableOpacity onPress={() => setBookingModalVisible(false)}>
                            <Ionicons name="close" size={28} color={colors.textPrimary} />
                        </TouchableOpacity>
                    </View>
                    
                    <ScrollView contentContainerStyle={styles.modalBody}>
                        <Text style={[typography.caption, { marginBottom: spacing.md }]}>
                            For this demo, time is hardcoded to today 09:00 - 18:00.
                        </Text>
                        <Input 
                            label="Allocation ID" 
                            value={allocationId} 
                            onChangeText={setAllocationId} 
                            placeholder="Enter lease GUID" 
                        />
                        <Input 
                            label="Vehicle Number" 
                            value={vehicleNumber} 
                            onChangeText={setVehicleNumber} 
                            placeholder="MH12AB1234" 
                        />
                        
                        <Button 
                            title="Confirm Booking" 
                            onPress={handleEmployeeBooking} 
                            loading={isBooking}
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
    bookingCard: {
        padding: spacing.md,
    },
    cancelBtn: {
        backgroundColor: colors.danger + '20',
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: 4,
    },
    cancelText: {
        color: colors.danger,
        fontSize: 12,
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

export default CorporateBookingsScreen;
