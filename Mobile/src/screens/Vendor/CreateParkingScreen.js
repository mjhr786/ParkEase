/**
 * CreateParkingScreen
 * Multi-section form to create a parking space
 */

import React, { useState, useCallback } from 'react';
import { View, Text, ScrollView, TouchableOpacity, StyleSheet, KeyboardAvoidingView, Platform, Image } from 'react-native';
import { EventBus } from '../../utils/EventBus';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { createParkingThunk, updateParkingThunk, uploadParkingImagesThunk, getMyListingsThunk } from '../../store/slices/parkingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { ParkingType, ParkingTypeLabels, AMENITIES } from '../../utils/constants';

const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;

const extractImageUrls = (listing) => {
    if (!listing) {
        return [];
    }

    const candidates = listing.imageUrls || listing.ImageUrls || listing.images || [];
    if (!Array.isArray(candidates)) {
        return [];
    }

    return candidates
        .map((item) => {
            if (typeof item === 'string') {
                return item.trim();
            }

            if (item && typeof item === 'object') {
                return (
                    item.publicUrl ||
                    item.imageUrl ||
                    item.url ||
                    item.ImageUrl ||
                    item.ImageURL ||
                    ''
                ).trim();
            }

            return '';
        })
        .filter(Boolean);
};

const imageListsMatch = (left, right) =>
    left.length === right.length && left.every((value, index) => value === right[index]);

const getImageName = (image) => image.fileName || image.uri?.split('/').pop() || 'Selected image';

const CreateParkingScreen = ({ route, navigation }) => {
    const { editData } = route.params || {};
    const isEditing = !!editData;
    const dispatch = useDispatch();
    const { createLoading } = useSelector((s) => s.parking);
    const initialExistingImageUrls = extractImageUrls(editData);
    const [selectedImages, setSelectedImages] = useState([]);
    const [existingImageUrls, setExistingImageUrls] = useState(initialExistingImageUrls);
    const [uploadStateByUri, setUploadStateByUri] = useState({});
    const [uploading, setUploading] = useState(false);

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

    const pickImage = async () => {
        const result = await ImagePicker.launchImageLibraryAsync({
            mediaTypes: ImagePicker.MediaTypeOptions.Images,
            allowsMultipleSelection: true,
            quality: 0.8,
        });

        if (!result.canceled) {
            const acceptedAssets = [];
            const rejectedFiles = [];

            result.assets.forEach((asset) => {
                if (asset.fileSize && asset.fileSize > MAX_IMAGE_SIZE_BYTES) {
                    rejectedFiles.push(asset.fileName || asset.uri?.split('/').pop() || 'Selected image');
                    return;
                }
                acceptedAssets.push(asset);
            });

            if (acceptedAssets.length > 0) {
                setSelectedImages((prev) => [...prev, ...acceptedAssets]);
                setUploadStateByUri((prev) => {
                    const next = { ...prev };

                    acceptedAssets.forEach((asset) => {
                        next[asset.uri] = {
                            status: 'pending',
                            progress: 0,
                            fileName: getImageName(asset),
                            error: null,
                        };
                    });

                    return next;
                });
            }

            if (rejectedFiles.length > 0) {
                EventBus.emit('SHOW_BANNER', {
                    title: 'Upload limit',
                    message: `${rejectedFiles.join(', ')} exceed the 5 MB image limit.`,
                    type: 'error'
                });
            }
        }
    };

    const removeImage = (uri) => {
        setSelectedImages((prev) => {
            const next = prev.filter((img) => img.uri !== uri);
            if (next.length !== prev.length) {
                setUploadStateByUri((current) => {
                    const nextState = { ...current };
                    delete nextState[uri];
                    return nextState;
                });
                return next;
            }
            setExistingImageUrls((current) => current.filter((imageUrl) => imageUrl !== uri));
            return prev;
        });
    };

    const displayedImages = [
        ...existingImageUrls.map((uri) => ({
            key: `remote-${uri}`,
            uri,
            isRemote: true,
            status: 'existing',
            statusLabel: 'Existing',
        })),
        ...selectedImages.map((img) => {
            const uploadState = uploadStateByUri[img.uri];
            const progress = uploadState?.progress || 0;
            const status = uploadState?.status || 'pending';
            const failedMessage = uploadState?.error;

            let statusLabel = 'Ready to upload';
            if (status === 'uploading') {
                statusLabel = progress >= 100 ? 'Saving...' : `Uploading ${progress}%`;
            } else if (status === 'uploaded') {
                statusLabel = 'Uploaded';
            } else if (status === 'failed') {
                statusLabel = failedMessage || 'Upload failed';
            }

            return {
                key: `local-${img.uri}`,
                uri: img.uri,
                isRemote: false,
                status,
                statusLabel,
            };
        }),
    ];

    const buildUploadCallbacks = useCallback(() => ({
        onFileStart: (image) => {
            setUploadStateByUri((prev) => ({
                ...prev,
                [image.uri]: {
                    ...(prev[image.uri] || {}),
                    status: 'uploading',
                    progress: 0,
                    fileName: getImageName(image),
                    error: null,
                },
            }));
        },
        onProgress: (image, progress) => {
            setUploadStateByUri((prev) => ({
                ...prev,
                [image.uri]: {
                    ...(prev[image.uri] || {}),
                    status: 'uploading',
                    progress,
                    fileName: getImageName(image),
                    error: null,
                },
            }));
        },
        onFileComplete: (image) => {
            setUploadStateByUri((prev) => ({
                ...prev,
                [image.uri]: {
                    ...(prev[image.uri] || {}),
                    status: 'uploaded',
                    progress: 100,
                    fileName: getImageName(image),
                    error: null,
                },
            }));
        },
        onFileError: ({ image, error }) => {
            if (!image?.uri) {
                return;
            }

            setUploadStateByUri((prev) => ({
                ...prev,
                [image.uri]: {
                    ...(prev[image.uri] || {}),
                    status: 'failed',
                    progress: 0,
                    fileName: getImageName(image),
                    error: typeof error === 'string' ? error : error?.message || 'Upload failed',
                },
            }));
        },
    }), []);

    const handleCreate = useCallback(async () => {
        if (!formData.title || !formData.description || !formData.address || !formData.city || !formData.state || !formData.totalSpots || !formData.hourlyRate) {
            EventBus.emit('SHOW_BANNER', { 
                title: 'Required Fields', 
                message: 'Please fill in Title, Description, Address, City, State, Total Spots, and Hourly Rate.',
                type: 'error'
            });
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
                
                const hasImageChanges =
                    selectedImages.length > 0 || !imageListsMatch(existingImageUrls, initialExistingImageUrls);

                if (hasImageChanges) {
                    setUploading(true);
                    try {
                        const uploadCallbacks = buildUploadCallbacks();
                        await dispatch(uploadParkingImagesThunk({ 
                            parkingSpaceId: editData.id, 
                            images: selectedImages,
                            existingImageUrls,
                            callbacks: uploadCallbacks,
                        })).unwrap();
                    } finally {
                        setUploading(false);
                    }
                }

                dispatch(getMyListingsThunk());
                EventBus.emit('SHOW_BANNER', { 
                    title: 'Success', 
                    message: 'Parking space updated!',
                    type: 'success'
                });
                
                // Delay navigation slightly so user sees the banner
                setTimeout(() => navigation.goBack(), 1500);
            } else {
                const result = await dispatch(createParkingThunk(payload)).unwrap();
                const newId = result.id || result._id;

                if (selectedImages.length > 0 && newId) {
                    setUploading(true);
                    try {
                        const uploadCallbacks = buildUploadCallbacks();
                        await dispatch(uploadParkingImagesThunk({ 
                            parkingSpaceId: newId, 
                            images: selectedImages,
                            existingImageUrls: [],
                            callbacks: uploadCallbacks,
                        })).unwrap();
                    } finally {
                        setUploading(false);
                    }
                }

                EventBus.emit('SHOW_BANNER', { 
                    title: 'Success', 
                    message: 'Parking space created!',
                    type: 'success'
                });
                
                // Delay navigation slightly so user sees the banner
                setTimeout(() => navigation.goBack(), 1500);
            }
        } catch (error) {
            EventBus.emit('SHOW_BANNER', { 
                title: 'Failed', 
                message: typeof error === 'string' ? error : `Could not ${isEditing ? 'update' : 'create'} parking space.`,
                type: 'error'
            });
        }
    }, [dispatch, formData, navigation, isEditing, editData, selectedImages, existingImageUrls, initialExistingImageUrls, buildUploadCallbacks]);

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

                    {/* Images Section */}
                    <Card>
                        <View style={styles.sectionHeader}>
                            <Text style={styles.sectionTitle}>Photos</Text>
                            <TouchableOpacity onPress={pickImage} style={styles.addImgBtn}>
                                <Ionicons name="camera-outline" size={20} color={colors.primary} />
                                <Text style={styles.addImgText}>Add Photos</Text>
                            </TouchableOpacity>
                        </View>
                        
                        {displayedImages.length > 0 ? (
                            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.imageScroll}>
                                {displayedImages.map((img) => (
                                    <View key={img.key} style={styles.imageContainer}>
                                        <Image source={{ uri: img.uri }} style={styles.previewImage} />
                                        <View
                                            style={[
                                                styles.imageStatusBadge,
                                                img.status === 'uploaded' && styles.imageStatusSuccess,
                                                img.status === 'failed' && styles.imageStatusError,
                                                img.status === 'uploading' && styles.imageStatusUploading,
                                            ]}
                                        >
                                            <Text
                                                numberOfLines={2}
                                                style={[
                                                    styles.imageStatusText,
                                                    img.status === 'uploaded' && styles.imageStatusSuccessText,
                                                    img.status === 'failed' && styles.imageStatusErrorText,
                                                ]}
                                            >
                                                {img.statusLabel}
                                            </Text>
                                        </View>
                                        <TouchableOpacity 
                                            style={styles.removeImgBtn}
                                            onPress={() => removeImage(img.uri)}
                                        >
                                            <Ionicons name="close-circle" size={22} color={colors.danger} />
                                        </TouchableOpacity>
                                    </View>
                                ))}
                            </ScrollView>
                        ) : (
                            <Text style={styles.emptyImgText}>No photos selected yet.</Text>
                        )}
                    </Card>

                    {/* Submit */}
                    <Button
                        title={isEditing ? "Save Changes" : "Create Parking Space"}
                        onPress={handleCreate}
                        loading={createLoading || uploading}
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
    sectionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.md },
    addImgBtn: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 12, paddingVertical: 6, borderRadius: spacing.radius.md, backgroundColor: colors.primarySoft },
    addImgText: { ...typography.caption, color: colors.primary, fontWeight: '600' },
    imageScroll: { marginTop: spacing.sm },
    imageContainer: { marginRight: spacing.md, position: 'relative' },
    previewImage: { width: 100, height: 100, borderRadius: spacing.radius.md, backgroundColor: colors.background },
    imageStatusBadge: {
        position: 'absolute',
        left: 6,
        right: 6,
        bottom: 6,
        paddingHorizontal: 8,
        paddingVertical: 4,
        borderRadius: spacing.radius.sm,
        backgroundColor: 'rgba(15, 23, 42, 0.72)',
    },
    imageStatusUploading: {
        backgroundColor: 'rgba(29, 78, 216, 0.84)',
    },
    imageStatusSuccess: {
        backgroundColor: 'rgba(22, 163, 74, 0.88)',
    },
    imageStatusError: {
        backgroundColor: 'rgba(220, 38, 38, 0.88)',
    },
    imageStatusText: {
        ...typography.caption,
        color: colors.white,
        fontSize: 11,
        fontWeight: '600',
        textAlign: 'center',
    },
    imageStatusSuccessText: {
        color: colors.white,
    },
    imageStatusErrorText: {
        color: colors.white,
    },
    removeImgBtn: { position: 'absolute', top: -8, right: -8, backgroundColor: colors.white, borderRadius: 11 },
    emptyImgText: { ...typography.caption, color: colors.textTertiary, textAlign: 'center', paddingVertical: spacing.md },
});

export default CreateParkingScreen;
