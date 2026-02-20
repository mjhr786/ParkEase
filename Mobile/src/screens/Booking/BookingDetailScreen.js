/**
 * BookingDetailScreen
 * Full booking details with actions: cancel, check-in/out
 */

import React, { useEffect, useCallback } from 'react';
import { View, Text, ScrollView, Alert, TouchableOpacity, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getBookingDetailThunk, cancelBookingThunk } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import Button from '../../components/Common/Button';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDateTime } from '../../utils/formatters';
import { BookingStatus, BookingStatusLabels, PricingTypeLabels, VehicleTypeLabels } from '../../utils/constants';

const InfoRow = ({ icon, label, value }) => (
    <View style={styles.infoRow}>
        <View style={styles.infoLeft}>
            <Ionicons name={icon} size={18} color={colors.primary} />
            <Text style={styles.infoLabel}>{label}</Text>
        </View>
        <Text style={styles.infoValue}>{value}</Text>
    </View>
);

const BookingDetailScreen = ({ navigation, route }) => {
    const { bookingId } = route.params;
    const dispatch = useDispatch();
    const { selectedBooking: booking, detailLoading, actionLoading } = useSelector((s) => s.booking);

    useEffect(() => {
        dispatch(getBookingDetailThunk(bookingId));
    }, [dispatch, bookingId]);

    const handleCancel = useCallback(() => {
        Alert.alert(
            'Cancel Booking',
            'Are you sure you want to cancel this booking?',
            [
                { text: 'No', style: 'cancel' },
                {
                    text: 'Yes, Cancel',
                    style: 'destructive',
                    onPress: () => dispatch(cancelBookingThunk({ id: bookingId, reason: 'Cancelled by user' })),
                },
            ]
        );
    }, [dispatch, bookingId]);

    if (detailLoading || !booking) return <LoadingScreen />;

    const canCancel = [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.AwaitingPayment].includes(booking.status);

    return (
        <ScreenLayout>
            <ScrollView showsVerticalScrollIndicator={false}>
                {/* Header */}
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                        <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                    </TouchableOpacity>
                    <Text style={styles.headerTitle}>Booking Details</Text>
                    <View style={{ width: 40 }} />
                </View>

                <View style={styles.content}>
                    {/* Status Banner */}
                    <Card style={styles.statusCard}>
                        <Badge status={booking.status} />
                        <Text style={styles.refCode}>Ref: {booking.bookingReference}</Text>
                    </Card>

                    {/* Parking Info */}
                    <Card>
                        <Text style={styles.sectionTitle}>Parking Location</Text>
                        <Text style={styles.parkingTitle}>{booking.parkingSpaceTitle}</Text>
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 4 }}>
                            <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                            <Text style={styles.parkingAddress}>{booking.parkingSpaceAddress || 'N/A'}</Text>
                        </View>
                    </Card>

                    {/* Booking Details */}
                    <Card>
                        <Text style={styles.sectionTitle}>Details</Text>
                        <InfoRow icon="calendar-outline" label="Start" value={formatDateTime(booking.startDateTime)} />
                        <InfoRow icon="calendar-outline" label="End" value={formatDateTime(booking.endDateTime)} />
                        <InfoRow icon="pricetag-outline" label="Pricing" value={PricingTypeLabels[booking.pricingType]} />
                        <InfoRow icon="car-outline" label="Vehicle" value={VehicleTypeLabels[booking.vehicleType] || 'N/A'} />
                    </Card>

                    {/* Payment */}
                    <Card style={styles.paymentCard}>
                        <Text style={styles.sectionTitle}>Payment</Text>
                        <View style={styles.totalRow}>
                            <Text style={styles.totalLabel}>Total Amount</Text>
                            <Text style={styles.totalValue}>{formatCurrency(booking.totalAmount)}</Text>
                        </View>
                    </Card>

                    {/* Actions */}
                    <View style={styles.actions}>
                        {canCancel && (
                            <Button title="Cancel Booking" onPress={handleCancel} variant="danger" loading={actionLoading} icon={<Ionicons name="close-circle" size={20} color={colors.white} />} />
                        )}
                        {booking.status === BookingStatus.Completed && (
                            <Button
                                title="Write Review"
                                onPress={() => navigation.navigate('CreateReview', { parkingSpaceId: booking.parkingSpaceId })}
                                variant="secondary"
                                icon={<Ionicons name="star" size={20} color={colors.primary} />}
                            />
                        )}
                    </View>
                </View>
            </ScrollView>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: 60, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base },
    backBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, justifyContent: 'center', alignItems: 'center', ...shadows.sm },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    content: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['3xl'] },
    statusCard: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    refCode: { ...typography.bodySmall, color: colors.textTertiary },
    sectionTitle: { ...typography.label, color: colors.textPrimary, marginBottom: spacing.md },
    parkingTitle: { ...typography.h4, color: colors.textPrimary },
    parkingAddress: { ...typography.caption, color: colors.textTertiary },
    infoRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.borderLight },
    infoLeft: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
    infoLabel: { ...typography.bodySmall, color: colors.textSecondary },
    infoValue: { ...typography.bodySmall, color: colors.textPrimary, fontWeight: '600' },
    paymentCard: { backgroundColor: colors.primarySoft },
    totalRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    totalLabel: { ...typography.body, color: colors.primary },
    totalValue: { ...typography.h3, color: colors.primary },
    actions: { gap: spacing.md, marginTop: spacing.lg },
});

export default BookingDetailScreen;
