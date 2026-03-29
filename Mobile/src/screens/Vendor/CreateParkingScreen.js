/**
 * CreateParkingScreen
 * Multi-section form to create a parking space
 */

import React, { useState, useCallback } from 'react';
import { View, Text, ScrollView, TouchableOpacity, Alert, StyleSheet, KeyboardAvoidingView, Platform } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { createParkingThunk, updateParkingThunk, getMyListingsThunk } from '../../store/slices/parkingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { ParkingType, ParkingTypeLabels, AMENITIES } from '../../utils/constants';

const CreateParkingScreen = ({ route, navigation }) => {
    const { editData } = route.params || {};
    const isEditing = !!editData;
    const dispatch = useDispatch();
    const { createLoading } = useSelector((s) => s.parking);

    const [formData, setFormData] = useState({
        title: editData?.title || '',
        description: editData?.description || '',
        address: editData?.address || '',
        city: editData?.city || '',
        state: editData?.state || '',
        country: editData?.country || 'India',
        postalCode: editData?.postalCode || '',
        latitude: editData?.latitude || 0,
        longitude: editData?.longitude || 0,
        totalSpots: editData?.totalSpots?.toString() || '',
        parkingType: editData?.parkingType || ParkingType.Open,
        hourlyRate: editData?.hourlyRate?.toString() || '',
        dailyRate: editData?.dailyRate?.toString() || '',
        weeklyRate: editData?.weeklyRate?.toString() || '',
        monthlyRate: editData?.monthlyRate?.toString() || '',
        is24Hours: editData?.is24Hours ?? true,
        amenities: editData?.amenities || [],
    });

    const updateField = (field) => (value) => {
        setFormData((prev) => ({ ...prev, [field]: value }));
    };

    const toggleAmenity = (amenity) => {
        setFormData((prev) => ({
            ...prev,
            amenities: prev.amenities.includes(amenity)
                ? prev.amenities.filter((a) => a !== amenity)
                : [...prev.amenities, amenity],
        }));
    };

    const handleCreate = useCallback(async () => {
        if (!formData.title || !formData.description || !formData.address || !formData.city || !formData.state || !formData.totalSpots || !formData.hourlyRate) {
            Alert.alert('Required Fields', 'Please fill in Title, Description, Address, City, State, Total Spots, and Hourly Rate.');
            return;
        }

        try {
            const payload = {
                ...formData,
                totalSpots: parseInt(formData.totalSpots),
                hourlyRate: parseFloat(formData.hourlyRate),
                dailyRate: parseFloat(formData.dailyRate) || 0,
                weeklyRate: parseFloat(formData.weeklyRate) || 0,
                monthlyRate: parseFloat(formData.monthlyRate) || 0,
            };

            if (isEditing) {
                await dispatch(updateParkingThunk({ id: editData.id, data: payload })).unwrap();
                dispatch(getMyListingsThunk()); // Refresh list safely
                Alert.alert('Success', 'Parking space updated!', [
                    { text: 'OK', onPress: () => navigation.goBack() },
                ]);
            } else {
                await dispatch(createParkingThunk(payload)).unwrap();
                Alert.alert('Success', 'Parking space created!', [
                    { text: 'OK', onPress: () => navigation.goBack() },
                ]);
            }
        } catch (error) {
            Alert.alert('Failed', typeof error === 'string' ? error : `Could not ${isEditing ? 'update' : 'create'} parking space. Please check all fields and try again.`);
        }
    }, [dispatch, formData, navigation, isEditing, editData]);

    return (
        <ScreenLayout>
            <KeyboardAvoidingView 
                style={{ flex: 1 }} 
                behavior={Platform.OS === 'ios' ? 'padding' : undefined}
            >
                <ScrollView 
                    showsVerticalScrollIndicator={false}
                    keyboardShouldPersistTaps="handled"
                >
                {/* Header */}
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                        <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                    </TouchableOpacity>
                    <Text style={styles.headerTitle}>{isEditing ? 'Edit Parking Space' : 'New Parking Space'}</Text>
                    <View style={{ width: 40 }} />
                </View>

                <View style={styles.content}>
                    {/* Basic Info */}
                    <Card>
                        <Text style={styles.sectionTitle}>Basic Information</Text>
                        <Input label="Title *" value={formData.title} onChangeText={updateField('title')} placeholder="e.g. Downtown Parking Garage" leftIcon="car-sport-outline" />
                        <Input label="Description *" value={formData.description} onChangeText={updateField('description')} placeholder="Describe your parking space" multiline numberOfLines={3} />
                        <Input label="Total Spots *" value={formData.totalSpots} onChangeText={updateField('totalSpots')} placeholder="Number of spots" keyboardType="numeric" leftIcon="grid-outline" />
                    </Card>

                    {/* Location */}
                    <Card>
                        <Text style={styles.sectionTitle}>Location</Text>
                        <Input label="Address *" value={formData.address} onChangeText={updateField('address')} placeholder="Street address" leftIcon="location-outline" />
                        <View style={styles.row}>
                            <View style={{ flex: 1 }}>
                                <Input label="City *" value={formData.city} onChangeText={updateField('city')} placeholder="City" />
                            </View>
                            <View style={{ flex: 1 }}>
                                <Input label="State *" value={formData.state} onChangeText={updateField('state')} placeholder="State" />
                            </View>
                        </View>
                        <View style={styles.row}>
                            <View style={{ flex: 1 }}>
                                <Input label="Country" value={formData.country} onChangeText={updateField('country')} placeholder="Country" />
                            </View>
                            <View style={{ flex: 1 }}>
                                <Input label="Postal Code" value={formData.postalCode} onChangeText={updateField('postalCode')} placeholder="Postal Code" keyboardType="numeric" />
                            </View>
                        </View>
                    </Card>

                    {/* Parking Type */}
                    <Card>
                        <Text style={styles.sectionTitle}>Parking Type</Text>
                        <View style={styles.chipRow}>
                            {Object.entries(ParkingTypeLabels).map(([value, label]) => (
                                <TouchableOpacity
                                    key={value}
                                    onPress={() => updateField('parkingType')(Number(value))}
                                    style={[styles.chip, formData.parkingType === Number(value) && styles.chipActive]}
                                >
                                    <Text style={[styles.chipText, formData.parkingType === Number(value) && styles.chipTextActive]}>{label}</Text>
                                </TouchableOpacity>
                            ))}
                        </View>
                    </Card>

                    {/* Pricing */}
                    <Card>
                        <Text style={styles.sectionTitle}>Pricing (₹)</Text>
                        <View style={styles.row}>
                            <View style={{ flex: 1 }}>
                                <Input label="Hourly *" value={formData.hourlyRate} onChangeText={updateField('hourlyRate')} placeholder="0" keyboardType="numeric" />
                            </View>
                            <View style={{ flex: 1 }}>
                                <Input label="Daily" value={formData.dailyRate} onChangeText={updateField('dailyRate')} placeholder="0" keyboardType="numeric" />
                            </View>
                        </View>
                        <View style={styles.row}>
                            <View style={{ flex: 1 }}>
                                <Input label="Weekly" value={formData.weeklyRate} onChangeText={updateField('weeklyRate')} placeholder="0" keyboardType="numeric" />
                            </View>
                            <View style={{ flex: 1 }}>
                                <Input label="Monthly" value={formData.monthlyRate} onChangeText={updateField('monthlyRate')} placeholder="0" keyboardType="numeric" />
                            </View>
                        </View>
                    </Card>

                    {/* Amenities */}
                    <Card>
                        <Text style={styles.sectionTitle}>Amenities</Text>
                        <View style={styles.amenitiesGrid}>
                            {AMENITIES.map((amenity) => (
                                <TouchableOpacity
                                    key={amenity}
                                    style={[styles.amenityChip, formData.amenities.includes(amenity) && styles.amenityChipActive]}
                                    onPress={() => toggleAmenity(amenity)}
                                >
                                    <Ionicons
                                        name={formData.amenities.includes(amenity) ? 'checkmark-circle' : 'ellipse-outline'}
                                        size={16}
                                        color={formData.amenities.includes(amenity) ? colors.success : colors.textTertiary}
                                    />
                                    <Text style={[styles.amenityText, formData.amenities.includes(amenity) && styles.amenityTextActive]}>
                                        {amenity}
                                    </Text>
                                </TouchableOpacity>
                            ))}
                        </View>
                    </Card>

                    {/* Submit */}
                    <Button
                        title={isEditing ? "Save Changes" : "Create Parking Space"}
                        onPress={handleCreate}
                        loading={createLoading}
                        style={styles.submitBtn}
                        icon={<Ionicons name={isEditing ? "save" : "checkmark-circle"} size={20} color={colors.white} />}
                    />
                </View>
            </ScrollView>
            </KeyboardAvoidingView>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: spacing.md, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base },
    backBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, justifyContent: 'center', alignItems: 'center', ...shadows.sm },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    content: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['3xl'] },
    sectionTitle: { ...typography.label, color: colors.textPrimary, marginBottom: spacing.md },
    row: { flexDirection: 'row', gap: spacing.md },
    chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
    chip: { paddingHorizontal: spacing.base, paddingVertical: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
    chipActive: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
    chipText: { ...typography.caption, color: colors.textSecondary, fontWeight: '500' },
    chipTextActive: { color: colors.primary, fontWeight: '600' },
    amenitiesGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
    amenityChip: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
    amenityChipActive: { backgroundColor: colors.successSoft, borderColor: colors.success },
    amenityText: { ...typography.caption, color: colors.textSecondary },
    amenityTextActive: { color: colors.successDark, fontWeight: '500' },
    submitBtn: { marginTop: spacing.lg },
});

export default CreateParkingScreen;
