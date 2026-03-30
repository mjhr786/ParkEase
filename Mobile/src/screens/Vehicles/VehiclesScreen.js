/**
 * VehiclesScreen
 * List of user's saved vehicles with add-vehicle form
 */

import React, { useEffect, useCallback, useState } from 'react';
import { EventBus } from '../../utils/EventBus';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, Alert, Modal } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getVehiclesThunk, addVehicleThunk, updateVehicleThunk, deleteVehicleThunk } from '../../store/slices/vehicleSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { VehiclesSkeleton } from '../../components/Common/ShimmerPlaceholder';
import EnhancedRefreshControl from '../../components/Common/EnhancedRefreshControl';
import SwipeableRow from '../../components/Common/SwipeableRow';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { VehicleTypeLabels, VehicleType } from '../../utils/constants';

const VehicleItem = ({ vehicle, onEdit }) => {
    const typeLabel = VehicleTypeLabels[vehicle.vehicleType] || 'Vehicle';
    const iconMap = {
        [VehicleType.Car]: 'car-outline',
        [VehicleType.Motorcycle]: 'bicycle-outline',
        [VehicleType.SUV]: 'car-sport-outline',
        [VehicleType.Truck]: 'bus-outline',
        [VehicleType.Van]: 'bus-outline',
        [VehicleType.Electric]: 'flash-outline',
    };

    return (
        <TouchableOpacity onPress={() => onEdit(vehicle)} activeOpacity={0.7}>
            <Card style={vStyles.card}>
                <View style={vStyles.row}>
                    <View style={vStyles.iconWrap}>
                        <Ionicons name={iconMap[vehicle.vehicleType] || 'car-outline'} size={24} color={colors.primary} />
                    </View>
                    <View style={vStyles.info}>
                        <Text style={vStyles.name}>{vehicle.make} {vehicle.model}</Text>
                        <Text style={vStyles.meta}>{typeLabel} · {vehicle.licensePlate}</Text>
                        {vehicle.color ? <Text style={vStyles.meta}>Color: {vehicle.color}</Text> : null}
                    </View>
                    <View style={vStyles.actions}>
                        <Ionicons name="create-outline" size={20} color={colors.primary} />
                    </View>
                </View>
            </Card>
        </TouchableOpacity>
    );
};

const vStyles = StyleSheet.create({
    card: { marginHorizontal: spacing.screenHorizontal },
    row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
    iconWrap: {
        width: 48,
        height: 48,
        borderRadius: 24,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
    },
    info: { flex: 1 },
    name: { ...typography.label, color: colors.textPrimary },
    meta: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    actions: { flexDirection: 'row', gap: spacing.sm },
});

const VehiclesScreen = ({ navigation, route }) => {
    const dispatch = useDispatch();
    const { vehicles, loading, addLoading } = useSelector((state) => state.vehicle);
    const [refreshing, setRefreshing] = useState(false);
    const [showModal, setShowModal] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [deletingId, setDeletingId] = useState(null);
    const [make, setMake] = useState('');
    const [model, setModel] = useState('');
    const [licensePlate, setLicensePlate] = useState('');
    const [vehicleColor, setVehicleColor] = useState('');
    const [vehicleType, setVehicleType] = useState(VehicleType.Car);

    useEffect(() => {
        dispatch(getVehiclesThunk());
        // Auto-open add modal if navigated with addNew param
        if (route?.params?.addNew) {
            openAddModal();
        }
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getVehiclesThunk());
        setRefreshing(false);
    }, [dispatch]);

    const openAddModal = () => {
        setEditingId(null);
        setMake(''); setModel(''); setLicensePlate(''); setVehicleColor(''); setVehicleType(VehicleType.Car);
        setShowModal(true);
    };

    const openEditModal = (vehicle) => {
        setEditingId(vehicle.id);
        setMake(vehicle.make); setModel(vehicle.model); setLicensePlate(vehicle.licensePlate); 
        setVehicleColor(vehicle.color || ''); setVehicleType(vehicle.vehicleType);
        setShowModal(true);
    };

    const handleDelete = (id) => {
        Alert.alert('Delete Vehicle', 'Are you sure you want to remove this vehicle?', [
            { text: 'Cancel', style: 'cancel' },
            { 
                text: 'Delete', 
                style: 'destructive', 
                onPress: async () => {
                    setDeletingId(id);
                    try {
                        const res = await dispatch(deleteVehicleThunk(id));
                        if (res.error) {
                            EventBus.emit('SHOW_ERROR_BANNER', { 
                                title: 'Error', 
                                message: res.payload || 'Failed to delete vehicle' 
                            });
                        } else {
                            EventBus.emit('SHOW_BANNER', { 
                                title: 'Success', 
                                message: 'Vehicle removed', 
                                type: 'success' 
                            });
                            onRefresh();
                        }
                    } finally {
                        setDeletingId(null);
                    }
                }
            }
        ]);
    };

    const handleSave = useCallback(async () => {
        if (!make.trim() || !model.trim() || !licensePlate.trim()) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Please fill in make, model, and license plate.' });
            return;
        }

        const payload = {
            make: make.trim(),
            model: model.trim(),
            licensePlate: licensePlate.trim(),
            color: vehicleColor.trim(),
            vehicleType,
        };

        let result;
        if (editingId) {
            result = await dispatch(updateVehicleThunk({ id: editingId, data: payload }));
        } else {
            result = await dispatch(addVehicleThunk(payload));
        }

        if (!result.error) {
            setShowModal(false);
            EventBus.emit('SHOW_BANNER', { 
                title: 'Success', 
                message: editingId ? 'Vehicle updated!' : 'Vehicle added!',
                type: 'success'
            });
            onRefresh();
        } else {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: result.payload || 'Failed to save vehicle' });
        }
    }, [dispatch, make, model, licensePlate, vehicleColor, vehicleType, editingId]);

    if (loading && !vehicles.length) {
        return (
            <ScreenLayout>
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()}>
                        <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                    </TouchableOpacity>
                    <Text style={styles.title}>Garage</Text>
                    <TouchableOpacity onPress={openAddModal}>
                        <Ionicons name="add-circle-outline" size={28} color={colors.primary} />
                    </TouchableOpacity>
                </View>
                <VehiclesSkeleton />
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <Text style={styles.title}>Garage</Text>
                <TouchableOpacity onPress={openAddModal}>
                    <Ionicons name="add-circle-outline" size={28} color={colors.primary} />
                </TouchableOpacity>
            </View>
            <FlatList
                data={vehicles}
                keyExtractor={(item) => item.id?.toString()}
                renderItem={({ item }) => (
                    <SwipeableRow 
                        onDelete={() => handleDelete(item.id)}
                        isDeleting={deletingId === item.id}
                        disabled={loading || !!deletingId}
                    >
                        <VehicleItem 
                            vehicle={item} 
                            onEdit={openEditModal} 
                        />
                    </SwipeableRow>
                )}
                ListEmptyComponent={
                    <EmptyState
                        icon="car-outline"
                        title="Your garage is empty"
                        message="Add a vehicle to speed up bookings"
                    />
                }
                refreshControl={
                    <EnhancedRefreshControl refreshing={refreshing} onRefresh={onRefresh} />
                }
                showsVerticalScrollIndicator={false}
                contentContainerStyle={{ paddingBottom: spacing['3xl'] }}
            />

            {/* Add Vehicle Modal */}
            <Modal visible={showModal} animationType="slide" transparent>
                <View style={styles.modalOverlay}>
                    <View style={styles.modalContent}>
                        <View style={styles.modalHeader}>
                            <Text style={styles.modalTitle}>{editingId ? 'Edit Vehicle' : 'Add Vehicle'}</Text>
                            <TouchableOpacity onPress={() => setShowModal(false)}>
                                <Ionicons name="close" size={24} color={colors.textPrimary} />
                            </TouchableOpacity>
                        </View>
                        <Input label="Make" placeholder="e.g. Toyota" value={make} onChangeText={setMake} leftIcon="car-outline" />
                        <Input label="Model" placeholder="e.g. Corolla" value={model} onChangeText={setModel} />
                        <Input label="License Plate" placeholder="e.g. ABC 1234" value={licensePlate} onChangeText={setLicensePlate} autoCapitalize="characters" />
                        <Input label="Color (optional)" placeholder="e.g. White" value={vehicleColor} onChangeText={setVehicleColor} />

                        <Text style={styles.typeLabel}>Vehicle Type</Text>
                        <View style={styles.typeRow}>
                            {Object.entries(VehicleTypeLabels).map(([key, label]) => (
                                <TouchableOpacity
                                    key={key}
                                    style={[styles.typeChip, vehicleType === Number(key) && styles.typeChipActive]}
                                    onPress={() => setVehicleType(Number(key))}
                                >
                                    <Text style={[styles.typeChipText, vehicleType === Number(key) && styles.typeChipTextActive]}>
                                        {label}
                                    </Text>
                                </TouchableOpacity>
                            ))}
                        </View>

                        <Button title={editingId ? 'Save Changes' : 'Add Vehicle'} onPress={handleSave} loading={addLoading} style={{ marginTop: spacing.lg }} />
                    </View>
                </View>
            </Modal>
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
    modalOverlay: {
        flex: 1,
        backgroundColor: 'rgba(0,0,0,0.5)',
        justifyContent: 'flex-end',
    },
    modalContent: {
        backgroundColor: colors.background,
        borderTopLeftRadius: spacing.radius.xl,
        borderTopRightRadius: spacing.radius.xl,
        padding: spacing.xl,
        paddingBottom: spacing['3xl'],
    },
    modalHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: spacing.lg,
    },
    modalTitle: { ...typography.h3, color: colors.textPrimary },
    typeLabel: { ...typography.label, color: colors.textPrimary, marginTop: spacing.md, marginBottom: spacing.sm },
    typeRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
    typeChip: {
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.xs,
        borderRadius: spacing.radius.full,
        backgroundColor: colors.surfaceAlt,
        borderWidth: 1,
        borderColor: colors.borderLight,
    },
    typeChipActive: {
        backgroundColor: colors.primarySoft,
        borderColor: colors.primary,
    },
    typeChipText: { ...typography.caption, color: colors.textSecondary },
    typeChipTextActive: { color: colors.primary, fontWeight: '600' },
});

export default VehiclesScreen;
