/**
 * BookingScreen
 * Create a booking with start/end date-time pickers, vehicle type, and price calculation
 */

import React, { useState, useCallback, useEffect } from 'react';
import { View, Text, ScrollView, TouchableOpacity, StyleSheet, Platform, Modal } from 'react-native';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { calculatePriceThunk, createBookingThunk, clearPriceBreakdown } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import Input from '../../components/Common/Input';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { PricingType, PricingTypeLabels, VehicleType, VehicleTypeLabels } from '../../utils/constants';

const BookingScreen = ({ navigation, route }) => {
    const { parkingId } = route.params;
    const dispatch = useDispatch();
    const { priceBreakdown, priceLoading, createLoading } = useSelector((s) => s.booking);
    const { selectedParking: parking } = useSelector((s) => s.parking);

    // Start time: next full hour
    const getInitialStart = () => {
        const d = new Date();
        d.setMinutes(0, 0, 0);
        d.setHours(d.getHours() + 1);
        return d;
    };
    // End time: start + 2 hours
    const getInitialEnd = (start) => {
        const d = new Date(start);
        d.setHours(d.getHours() + 2);
        return d;
    };

    const [startDate, setStartDate] = useState(getInitialStart());
    const [endDate, setEndDate] = useState(getInitialEnd(getInitialStart()));
    const [pricingType, setPricingType] = useState(PricingType.Hourly);
    const [vehicleType, setVehicleType] = useState(VehicleType.Car);
    const [discountCode, setDiscountCode] = useState('');

    // Picker visibility (iOS uses modal, Android uses inline)
    const [showPicker, setShowPicker] = useState(null); // 'startDate' | 'startTime' | 'endDate' | 'endTime' | null

    // Calculate price whenever dates or pricing type change
    useEffect(() => {
        if (startDate < endDate) {
            dispatch(calculatePriceThunk({
                parkingSpaceId: parkingId,
                startDateTime: startDate.toISOString(),
                endDateTime: endDate.toISOString(),
                pricingType,
                discountCode: discountCode || undefined,
            }));
        }
    }, [startDate, endDate, pricingType]);

    useEffect(() => {
        return () => { dispatch(clearPriceBreakdown()); };
    }, []);

    const handlePickerChange = (pickerType) => (event, selectedDate) => {
        if (Platform.OS === 'android') {
            setShowPicker(null);
        }
        if (event.type === 'dismissed') return;
        if (!selectedDate) return;

        if (pickerType === 'startDate') {
            const updated = new Date(startDate);
            updated.setFullYear(selectedDate.getFullYear(), selectedDate.getMonth(), selectedDate.getDate());
            setStartDate(updated);
            // Ensure end is after start
            if (updated >= endDate) {
                const newEnd = new Date(updated);
                newEnd.setHours(newEnd.getHours() + 2);
                setEndDate(newEnd);
            }
        } else if (pickerType === 'startTime') {
            const updated = new Date(startDate);
            updated.setHours(selectedDate.getHours(), selectedDate.getMinutes(), 0, 0);
            setStartDate(updated);
            if (updated >= endDate) {
                const newEnd = new Date(updated);
                newEnd.setHours(newEnd.getHours() + 2);
                setEndDate(newEnd);
            }
        } else if (pickerType === 'endDate') {
            const updated = new Date(endDate);
            updated.setFullYear(selectedDate.getFullYear(), selectedDate.getMonth(), selectedDate.getDate());
            if (updated > startDate) setEndDate(updated);
        } else if (pickerType === 'endTime') {
            const updated = new Date(endDate);
            updated.setHours(selectedDate.getHours(), selectedDate.getMinutes(), 0, 0);
            if (updated > startDate) setEndDate(updated);
        }
    };

    const handleConfirmBooking = async () => {
        if (startDate >= endDate) return;
        const result = await dispatch(createBookingThunk({
            parkingSpaceId: parkingId,
            startDateTime: startDate.toISOString(),
            endDateTime: endDate.toISOString(),
            pricingType,
            vehicleType,
            discountCode: discountCode || undefined,
        }));
        if (!result.error) {
            navigation.goBack();
        }
    };

    const getDurationText = () => {
        const diffMs = endDate - startDate;
        const totalMinutes = Math.floor(diffMs / (1000 * 60));
        const hours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;
        if (hours === 0) return `${minutes}m`;
        if (minutes === 0) return `${hours}h`;
        return `${hours}h ${minutes}m`;
    };

    const isValidRange = startDate < endDate && startDate >= new Date(Date.now() - 60000);

    // Date-time selector button
    const DateTimeButton = ({ label, dateText, timeText, onDatePress, onTimePress }) => (
        <View style={styles.dtGroup}>
            <Text style={styles.dtLabel}>{label}</Text>
            <View style={styles.dtRow}>
                <TouchableOpacity style={styles.dtButton} onPress={onDatePress}>
                    <Ionicons name="calendar-outline" size={18} color={colors.primary} />
                    <Text style={styles.dtButtonText}>{dateText}</Text>
                </TouchableOpacity>
                <TouchableOpacity style={styles.dtButton} onPress={onTimePress}>
                    <Ionicons name="time-outline" size={18} color={colors.primary} />
                    <Text style={styles.dtButtonText}>{timeText}</Text>
                </TouchableOpacity>
            </View>
        </View>
    );

    const renderPicker = () => {
        if (!showPicker) return null;
        const isStart = showPicker.startsWith('start');
        const isDate = showPicker.endsWith('Date');
        const currentValue = isStart ? startDate : endDate;

        if (Platform.OS === 'ios') {
            return (
                <Modal transparent animationType="slide" visible={!!showPicker}>
                    <View style={styles.modalOverlay}>
                        <View style={styles.modalContent}>
                            <View style={styles.modalHeader}>
                                <Text style={styles.modalTitle}>
                                    Select {isStart ? 'Start' : 'End'} {isDate ? 'Date' : 'Time'}
                                </Text>
                                <TouchableOpacity onPress={() => setShowPicker(null)}>
                                    <Text style={styles.modalDone}>Done</Text>
                                </TouchableOpacity>
                            </View>
                            <DateTimePicker
                                value={currentValue}
                                mode={isDate ? 'date' : 'time'}
                                display="spinner"
                                onChange={handlePickerChange(showPicker)}
                                minimumDate={isStart ? new Date() : startDate}
                                minuteInterval={15}
                                style={{ height: 200 }}
                            />
                        </View>
                    </View>
                </Modal>
            );
        }

        // Android inline
        return (
            <DateTimePicker
                value={currentValue}
                mode={isDate ? 'date' : 'time'}
                display="default"
                onChange={handlePickerChange(showPicker)}
                minimumDate={isStart ? new Date() : startDate}
                minuteInterval={15}
            />
        );
    };

    return (
        <ScreenLayout>
            <ScrollView showsVerticalScrollIndicator={false}>
                {/* Header */}
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                        <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                    </TouchableOpacity>
                    <Text style={styles.headerTitle}>Book Parking</Text>
                    <View style={{ width: 40 }} />
                </View>

                <View style={styles.content}>
                    {/* Parking Info */}
                    <Card>
                        <Text style={styles.parkingTitle}>{parking?.title}</Text>
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 4 }}>
                            <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                            <Text style={styles.parkingAddress}>{parking?.address}</Text>
                        </View>
                    </Card>

                    {/* Date & Time Selection */}
                    <Card>
                        <Text style={styles.sectionTitle}>When do you need parking?</Text>

                        <DateTimeButton
                            label="Start"
                            dateText={formatDate(startDate)}
                            timeText={formatTime(startDate)}
                            onDatePress={() => setShowPicker('startDate')}
                            onTimePress={() => setShowPicker('startTime')}
                        />

                        <View style={styles.dtDivider}>
                            <View style={styles.dtDividerLine} />
                            <View style={styles.durationBadge}>
                                <Ionicons name="time-outline" size={14} color={colors.white} />
                                <Text style={styles.durationBadgeText}>{getDurationText()}</Text>
                            </View>
                            <View style={styles.dtDividerLine} />
                        </View>

                        <DateTimeButton
                            label="End"
                            dateText={formatDate(endDate)}
                            timeText={formatTime(endDate)}
                            onDatePress={() => setShowPicker('endDate')}
                            onTimePress={() => setShowPicker('endTime')}
                        />

                        {!isValidRange && (
                            <View style={styles.warningBanner}>
                                <Ionicons name="warning-outline" size={16} color={colors.accent} />
                                <Text style={styles.warningText}>End time must be after start time</Text>
                            </View>
                        )}
                    </Card>

                    {/* Pricing Type */}
                    <Card>
                        <Text style={styles.sectionTitle}>Pricing Type</Text>
                        <View style={styles.chipRow}>
                            {Object.entries(PricingTypeLabels).map(([value, label]) => (
                                <TouchableOpacity
                                    key={value}
                                    onPress={() => setPricingType(Number(value))}
                                    style={[styles.chip, pricingType === Number(value) && styles.chipActive]}
                                >
                                    <Text style={[styles.chipText, pricingType === Number(value) && styles.chipTextActive]}>{label}</Text>
                                </TouchableOpacity>
                            ))}
                        </View>
                    </Card>

                    {/* Vehicle Type */}
                    <Card>
                        <Text style={styles.sectionTitle}>Vehicle Type</Text>
                        <View style={styles.chipRow}>
                            {Object.entries(VehicleTypeLabels).map(([value, label]) => (
                                <TouchableOpacity
                                    key={value}
                                    onPress={() => setVehicleType(Number(value))}
                                    style={[styles.chip, vehicleType === Number(value) && styles.chipActive]}
                                >
                                    <Text style={[styles.chipText, vehicleType === Number(value) && styles.chipTextActive]}>{label}</Text>
                                </TouchableOpacity>
                            ))}
                        </View>
                    </Card>

                    {/* Discount */}
                    <Card>
                        <Input
                            label="Discount Code (optional)"
                            value={discountCode}
                            onChangeText={setDiscountCode}
                            placeholder="Enter code"
                            leftIcon="pricetag-outline"
                            style={{ marginBottom: 0 }}
                        />
                    </Card>

                    {/* Price Breakdown */}
                    <Card style={styles.priceCard}>
                        <Text style={styles.sectionTitle}>Price Summary</Text>
                        {priceLoading ? (
                            <Text style={styles.calculating}>Calculating...</Text>
                        ) : priceBreakdown ? (
                            <View>
                                <View style={styles.priceRow}>
                                    <Text style={styles.priceLabel}>Base price</Text>
                                    <Text style={styles.priceValue}>{formatCurrency(priceBreakdown.basePrice)}</Text>
                                </View>
                                {priceBreakdown.discount > 0 && (
                                    <View style={styles.priceRow}>
                                        <Text style={[styles.priceLabel, { color: colors.success }]}>Discount</Text>
                                        <Text style={[styles.priceValue, { color: colors.success }]}>-{formatCurrency(priceBreakdown.discount)}</Text>
                                    </View>
                                )}
                                <View style={[styles.priceRow, styles.totalRow]}>
                                    <Text style={styles.totalLabel}>Total</Text>
                                    <Text style={styles.totalValue}>{formatCurrency(priceBreakdown.totalPrice)}</Text>
                                </View>
                            </View>
                        ) : !isValidRange ? (
                            <Text style={styles.calculating}>Select valid date range to see price</Text>
                        ) : null}
                    </Card>

                    {/* Confirm */}
                    <Button
                        title="Confirm Booking"
                        onPress={handleConfirmBooking}
                        loading={createLoading}
                        disabled={!isValidRange}
                        style={[styles.confirmBtn, !isValidRange && { opacity: 0.5 }]}
                        icon={<Ionicons name="checkmark-circle" size={20} color={colors.white} />}
                    />
                </View>
            </ScrollView>

            {/* Date/Time Picker */}
            {renderPicker()}
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: 60, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base },
    backBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, justifyContent: 'center', alignItems: 'center', ...shadows.sm },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    content: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['3xl'] },
    parkingTitle: { ...typography.h4, color: colors.textPrimary },
    parkingAddress: { ...typography.caption, color: colors.textTertiary },
    sectionTitle: { ...typography.label, color: colors.textPrimary, marginBottom: spacing.md },

    // Date-time picker styles
    dtGroup: { marginBottom: spacing.sm },
    dtLabel: { ...typography.caption, color: colors.textSecondary, fontWeight: '600', textTransform: 'uppercase', letterSpacing: 1, marginBottom: spacing.xs },
    dtRow: { flexDirection: 'row', gap: spacing.sm },
    dtButton: {
        flex: 1, flexDirection: 'row', alignItems: 'center', gap: spacing.sm,
        backgroundColor: colors.background, borderRadius: spacing.radius.lg,
        paddingHorizontal: spacing.base, paddingVertical: spacing.md,
        borderWidth: 1, borderColor: colors.border,
    },
    dtButtonText: { ...typography.body, color: colors.textPrimary, fontWeight: '500' },
    dtDivider: { flexDirection: 'row', alignItems: 'center', marginVertical: spacing.md },
    dtDividerLine: { flex: 1, height: 1, backgroundColor: colors.border },
    durationBadge: {
        flexDirection: 'row', alignItems: 'center', gap: 4,
        backgroundColor: colors.primary, borderRadius: spacing.radius.full,
        paddingHorizontal: spacing.base, paddingVertical: 4,
        marginHorizontal: spacing.sm,
    },
    durationBadgeText: { ...typography.caption, color: colors.white, fontWeight: '700' },
    warningBanner: {
        flexDirection: 'row', alignItems: 'center', gap: spacing.sm,
        backgroundColor: colors.accentSoft, padding: spacing.sm,
        borderRadius: spacing.radius.md, marginTop: spacing.sm,
    },
    warningText: { ...typography.caption, color: colors.accentDark },

    // Modal picker (iOS)
    modalOverlay: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0,0,0,0.4)' },
    modalContent: { backgroundColor: colors.white, borderTopLeftRadius: 20, borderTopRightRadius: 20, paddingBottom: 30 },
    modalHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: spacing.base, borderBottomWidth: 1, borderBottomColor: colors.border },
    modalTitle: { ...typography.h4, color: colors.textPrimary },
    modalDone: { ...typography.body, color: colors.primary, fontWeight: '600' },

    // Chips
    chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
    chip: { paddingHorizontal: spacing.base, paddingVertical: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
    chipActive: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
    chipText: { ...typography.caption, color: colors.textSecondary, fontWeight: '500' },
    chipTextActive: { color: colors.primary, fontWeight: '600' },

    // Price
    priceCard: { backgroundColor: colors.primarySoft },
    calculating: { ...typography.bodySmall, color: colors.textSecondary, textAlign: 'center' },
    priceRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: spacing.xs },
    priceLabel: { ...typography.bodySmall, color: colors.textSecondary },
    priceValue: { ...typography.bodySmall, color: colors.textPrimary, fontWeight: '600' },
    totalRow: { borderTopWidth: 1, borderTopColor: colors.primary, marginTop: spacing.sm, paddingTop: spacing.sm },
    totalLabel: { ...typography.h4, color: colors.primary },
    totalValue: { ...typography.h3, color: colors.primary },
    confirmBtn: { marginTop: spacing.base },
});

export default BookingScreen;
