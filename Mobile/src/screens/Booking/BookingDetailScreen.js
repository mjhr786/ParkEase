/**
 * BookingDetailScreen
 * Full booking details with ownership-based actions:
 * Listing owner: approve/reject/manage extensions
 * Booker: cancel/pay/extend/review
 */

import React, { useEffect, useCallback, useState } from 'react';
import { EventBus } from '../../utils/EventBus';
import {
    View, Text, ScrollView, Alert, TouchableOpacity, StyleSheet,
    Modal, TextInput, KeyboardAvoidingView, Platform
} from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import {
    getBookingDetailThunk,
    cancelBookingThunk,
    approveBookingThunk,
    rejectBookingThunk,
    checkInBookingThunk,
    checkOutBookingThunk,
    extendBookingThunk,
    approveExtensionThunk,
    rejectExtensionThunk,
} from '../../store/slices/bookingSlice';
import { useAuth } from '../../hooks/useAuth';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import Button from '../../components/Common/Button';
import { DetailSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDateTime } from '../../utils/formatters';
import { BookingStatus, BookingStatusLabels, PricingTypeLabels, VehicleTypeLabels } from '../../utils/constants';
import chatService from '../../services/chat/chatService';

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
    const { isOwnerOfBooking, user } = useAuth();
    const { selectedBooking: booking, detailLoading } = useSelector((s) => s.booking);
    const [actionLoading, setActionLoading] = useState(false);
    const [rejectModalVisible, setRejectModalVisible] = useState(false);
    const [rejectReason, setRejectReason] = useState('');
    const [chatLoading, setChatLoading] = useState(false);
    const [extendModalVisible, setExtendModalVisible] = useState(false);
    const [extendHours, setExtendHours] = useState('');

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
                    onPress: async () => {
                        setActionLoading(true);
                        try {
                            await dispatch(cancelBookingThunk({ id: bookingId, reason: 'Cancelled by user' })).unwrap();
                            Alert.alert('Success', 'Booking has been cancelled.');
                        } catch (error) {
                            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to cancel booking' });
                        } finally {
                            setActionLoading(false);
                        }
                    },
                },
            ]
        );
    }, [dispatch, bookingId]);

    const handleApprove = useCallback(() => {
        Alert.alert('Approve Booking', 'Confirm approval of this booking?', [
            { text: 'Cancel', style: 'cancel' },
            {
                text: 'Approve',
                onPress: async () => {
                    setActionLoading(true);
                    try {
                        await dispatch(approveBookingThunk(bookingId)).unwrap();
                        Alert.alert('Success', 'Booking approved successfully!');
                        dispatch(getBookingDetailThunk(bookingId));
                    } catch (error) {
                        EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to approve booking' });
                    } finally {
                        setActionLoading(false);
                    }
                },
            },
        ]);
    }, [dispatch, bookingId]);

    const handleReject = useCallback(async () => {
        setActionLoading(true);
        try {
            await dispatch(rejectBookingThunk({
                id: bookingId,
                reason: rejectReason.trim() || 'Rejected by vendor',
            })).unwrap();
            Alert.alert('Done', 'Booking has been rejected.');
            setRejectModalVisible(false);
            setRejectReason('');
            dispatch(getBookingDetailThunk(bookingId));
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to reject booking' });
        } finally {
            setActionLoading(false);
        }
    }, [dispatch, bookingId, rejectReason]);

    const handleChat = useCallback(async () => {
        if (!booking) return;
        setChatLoading(true);
        try {
            const existing = await chatService.findConversationByParkingSpace(booking.parkingSpaceId);
            const chatParams = {
                parkingSpaceId: booking.parkingSpaceId,
                participantName: isOwner ? (booking.userName || 'User') : (booking.ownerName || 'Owner'),
                parkingTitle: booking.parkingSpaceTitle || 'Parking Space',
            };
            if (existing) {
                navigation.navigate('ChatScreen', { conversationId: existing.id, ...chatParams });
            } else {
                navigation.navigate('ChatScreen', { conversationId: null, ...chatParams });
            }
        } catch {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Could not open chat.' });
        } finally {
            setChatLoading(false);
        }
    }, [booking, navigation]);

    const handleCheckIn = useCallback(async () => {
        setActionLoading(true);
        try {
            await dispatch(checkInBookingThunk(bookingId)).unwrap();
            Alert.alert('Success', 'Checked in successfully!');
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to check in' });
        } finally {
            setActionLoading(false);
        }
    }, [dispatch, bookingId]);

    const handleCheckOut = useCallback(async () => {
        Alert.alert('Check Out', 'Confirm check-out from this parking space?', [
            { text: 'Cancel', style: 'cancel' },
            {
                text: 'Check Out',
                onPress: async () => {
                    setActionLoading(true);
                    try {
                        await dispatch(checkOutBookingThunk(bookingId)).unwrap();
                        Alert.alert('Success', 'Checked out successfully!');
                    } catch (error) {
                        EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to check out' });
                    } finally {
                        setActionLoading(false);
                    }
                },
            },
        ]);
    }, [dispatch, bookingId]);

    const handleExtend = useCallback(async () => {
        const hours = parseInt(extendHours, 10);
        if (!hours || hours < 1) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Please enter a valid number of hours.' });
            return;
        }
        setActionLoading(true);
        try {
            await dispatch(extendBookingThunk({ id: bookingId, data: { additionalHours: hours } })).unwrap();
            Alert.alert('Success', 'Extension request submitted!');
            setExtendModalVisible(false);
            setExtendHours('');
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to request extension' });
        } finally {
            setActionLoading(false);
        }
    }, [dispatch, bookingId, extendHours]);

    const handleApproveExtension = useCallback(async () => {
        setActionLoading(true);
        try {
            await dispatch(approveExtensionThunk(bookingId)).unwrap();
            Alert.alert('Success', 'Extension approved!');
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to approve extension' });
        } finally {
            setActionLoading(false);
        }
    }, [dispatch, bookingId]);

    const handleRejectExtension = useCallback(async () => {
        setActionLoading(true);
        try {
            await dispatch(rejectExtensionThunk(bookingId)).unwrap();
            Alert.alert('Done', 'Extension rejected.');
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to reject extension' });
        } finally {
            setActionLoading(false);
        }
    }, [dispatch, bookingId]);

    if (detailLoading || !booking) return <DetailSkeleton />;

    const isOwner = isOwnerOfBooking(booking);

    const canCancel = !isOwner && [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.AwaitingPayment].includes(booking.status);
    const canApproveReject = isOwner && booking.status === BookingStatus.Pending;
    const canCheckIn = booking.status === BookingStatus.Confirmed;
    const canCheckOut = booking.status === BookingStatus.InProgress;
    const canExtend = !isOwner && booking.status === BookingStatus.InProgress;
    const hasExtensionPending = booking.extensionStatus === 'Pending' || booking.hasExtensionRequest;
    const canManageExtension = isOwner && hasExtensionPending;

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

                    {/* User info (for listing owner view) */}
                    {isOwner && booking.userName && (
                        <Card>
                            <Text style={styles.sectionTitle}>Booked By</Text>
                            <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                                <View style={styles.userAvatar}>
                                    <Text style={styles.userAvatarText}>
                                        {booking.userName?.charAt(0)?.toUpperCase() || '?'}
                                    </Text>
                                </View>
                                <View>
                                    <Text style={styles.userName}>{booking.userName}</Text>
                                    {booking.vehicleNumber && (
                                        <Text style={styles.vehicleNumber}>🚗 {booking.vehicleNumber}</Text>
                                    )}
                                </View>
                            </View>
                        </Card>
                    )}

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
                        {/* Vendor: Approve / Reject for pending bookings */}
                        {canApproveReject && (
                            <View style={styles.vendorActions}>
                                <Button
                                    title="Approve"
                                    onPress={handleApprove}
                                    loading={actionLoading}
                                    style={{ flex: 1 }}
                                    icon={<Ionicons name="checkmark-circle" size={20} color={colors.white} />}
                                />
                                <Button
                                    title="Reject"
                                    onPress={() => {
                                        setRejectReason('');
                                        setRejectModalVisible(true);
                                    }}
                                    variant="danger"
                                    style={{ flex: 1 }}
                                    icon={<Ionicons name="close-circle" size={20} color={colors.white} />}
                                />
                            </View>
                        )}

                        {/* Member: Cancel */}
                        {canCancel && (
                            <Button
                                title="Cancel Booking"
                                onPress={handleCancel}
                                variant="danger"
                                loading={actionLoading}
                                icon={<Ionicons name="close-circle" size={20} color={colors.white} />}
                            />
                        )}

                        {/* Check In */}
                        {canCheckIn && (
                            <Button
                                title="Check In"
                                onPress={handleCheckIn}
                                loading={actionLoading}
                                icon={<Ionicons name="log-in" size={20} color={colors.white} />}
                            />
                        )}

                        {/* Check Out */}
                        {canCheckOut && (
                            <Button
                                title="Check Out"
                                onPress={handleCheckOut}
                                loading={actionLoading}
                                variant="secondary"
                                icon={<Ionicons name="log-out" size={20} color={colors.primary} />}
                            />
                        )}

                        {/* Extend */}
                        {canExtend && (
                            <Button
                                title="Extend Booking"
                                onPress={() => { setExtendHours(''); setExtendModalVisible(true); }}
                                variant="secondary"
                                icon={<Ionicons name="time" size={20} color={colors.primary} />}
                            />
                        )}

                        {/* Vendor: Approve/Reject Extension */}
                        {canManageExtension && (
                            <Card>
                                <Text style={styles.sectionTitle}>Extension Request</Text>
                                <Text style={{ ...typography.bodySmall, color: colors.textSecondary, marginBottom: spacing.md }}>
                                    The user has requested a booking extension.
                                </Text>
                                <View style={styles.vendorActions}>
                                    <Button title="Approve" onPress={handleApproveExtension} loading={actionLoading} style={{ flex: 1 }} />
                                    <Button title="Reject" onPress={handleRejectExtension} variant="danger" loading={actionLoading} style={{ flex: 1 }} />
                                </View>
                            </Card>
                        )}

                        {/* Payment - for awaiting payment bookings */}
                        {!isOwner && booking.status === BookingStatus.AwaitingPayment && (
                            <Button
                                title="Proceed to Payment"
                                onPress={() => navigation.navigate('PaymentScreen', {
                                    bookingId: booking.id,
                                    amount: booking.totalAmount,
                                    parkingTitle: booking.parkingSpaceTitle,
                                    priceBreakdown: {
                                        duration: booking.durationText || `${booking.startDateTime ? new Date(booking.startDateTime).toLocaleDateString() : ''} – ${booking.endDateTime ? new Date(booking.endDateTime).toLocaleDateString() : ''}`,
                                        basePrice: booking.baseAmount || booking.totalAmount,
                                        serviceFee: booking.serviceFee || 0,
                                    },
                                })}
                                icon={<Ionicons name="card" size={20} color={colors.white} />}
                            />
                        )}

                        {/* Chat button for both roles */}
                        <Button
                            title={isOwner ? 'Chat with Booker' : 'Chat with Owner'}
                            onPress={handleChat}
                            variant="secondary"
                            loading={chatLoading}
                            icon={<Ionicons name="chatbubble-ellipses" size={20} color={colors.primary} />}
                        />

                        {/* Review button for completed bookings (booker only) */}
                        {!isOwner && booking.status === BookingStatus.Completed && (
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

            {/* Reject Reason Modal */}
            <Modal
                visible={rejectModalVisible}
                transparent
                animationType="fade"
                onRequestClose={() => setRejectModalVisible(false)}
            >
                <KeyboardAvoidingView
                    style={styles.modalOverlay}
                    behavior={Platform.OS === 'ios' ? 'padding' : undefined}
                >
                    <View style={styles.modalContent}>
                        <Text style={styles.modalTitle}>Reject Booking</Text>
                        <Text style={styles.modalSubtitle}>
                            Provide a reason for rejection (optional):
                        </Text>
                        <TextInput
                            style={styles.modalInput}
                            value={rejectReason}
                            onChangeText={setRejectReason}
                            placeholder="Reason for rejection..."
                            placeholderTextColor={colors.textTertiary}
                            multiline
                            numberOfLines={3}
                            textAlignVertical="top"
                        />
                        <View style={styles.modalActions}>
                            <Button
                                title="Confirm Reject"
                                onPress={handleReject}
                                variant="danger"
                                loading={actionLoading}
                                style={{ flex: 1 }}
                            />
                            <Button
                                title="Cancel"
                                onPress={() => {
                                    setRejectModalVisible(false);
                                    setRejectReason('');
                                }}
                                variant="secondary"
                                style={{ flex: 1 }}
                            />
                        </View>
                    </View>
                </KeyboardAvoidingView>
            </Modal>

            {/* Extend Booking Modal */}
            <Modal
                visible={extendModalVisible}
                transparent
                animationType="fade"
                onRequestClose={() => setExtendModalVisible(false)}
            >
                <KeyboardAvoidingView
                    style={styles.modalOverlay}
                    behavior={Platform.OS === 'ios' ? 'padding' : undefined}
                >
                    <View style={styles.modalContent}>
                        <Text style={styles.modalTitle}>Extend Booking</Text>
                        <Text style={styles.modalSubtitle}>
                            How many additional hours?
                        </Text>
                        <TextInput
                            style={styles.modalInput}
                            value={extendHours}
                            onChangeText={setExtendHours}
                            placeholder="Number of hours"
                            placeholderTextColor={colors.textTertiary}
                            keyboardType="numeric"
                        />
                        <View style={styles.modalActions}>
                            <Button
                                title="Request Extension"
                                onPress={handleExtend}
                                loading={actionLoading}
                                style={{ flex: 1 }}
                            />
                            <Button
                                title="Cancel"
                                onPress={() => setExtendModalVisible(false)}
                                variant="secondary"
                                style={{ flex: 1 }}
                            />
                        </View>
                    </View>
                </KeyboardAvoidingView>
            </Modal>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: spacing.md, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.base },
    backBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, justifyContent: 'center', alignItems: 'center', ...shadows.sm },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    content: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['3xl'] },
    statusCard: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    refCode: { ...typography.bodySmall, color: colors.textTertiary },
    sectionTitle: { ...typography.label, color: colors.textPrimary, marginBottom: spacing.md },
    parkingTitle: { ...typography.h4, color: colors.textPrimary },
    parkingAddress: { ...typography.caption, color: colors.textTertiary },
    userAvatar: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.primary, justifyContent: 'center', alignItems: 'center' },
    userAvatarText: { color: '#fff', fontSize: 16, fontWeight: '700' },
    userName: { ...typography.label, color: colors.textPrimary },
    vehicleNumber: { ...typography.caption, color: colors.textSecondary, marginTop: 2 },
    infoRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.borderLight },
    infoLeft: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
    infoLabel: { ...typography.bodySmall, color: colors.textSecondary },
    infoValue: { ...typography.bodySmall, color: colors.textPrimary, fontWeight: '600' },
    paymentCard: { backgroundColor: colors.primarySoft },
    totalRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    totalLabel: { ...typography.body, color: colors.primary },
    totalValue: { ...typography.h3, color: colors.primary },
    actions: { gap: spacing.md, marginTop: spacing.lg },
    vendorActions: { flexDirection: 'row', gap: spacing.md },
    // Modal
    modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'center', alignItems: 'center', padding: spacing.lg },
    modalContent: { backgroundColor: colors.surface, borderRadius: spacing.radius.xl, padding: spacing.xl, width: '100%', maxWidth: 400 },
    modalTitle: { ...typography.h3, color: colors.textPrimary, marginBottom: spacing.xs },
    modalSubtitle: { ...typography.bodySmall, color: colors.textSecondary, marginBottom: spacing.md },
    modalInput: {
        backgroundColor: colors.background, borderRadius: spacing.radius.md,
        borderWidth: 1, borderColor: colors.border, padding: spacing.md,
        fontSize: 14, color: colors.text, minHeight: 80, marginBottom: spacing.md,
    },
    modalActions: { flexDirection: 'row', gap: spacing.md },
});

export default BookingDetailScreen;
