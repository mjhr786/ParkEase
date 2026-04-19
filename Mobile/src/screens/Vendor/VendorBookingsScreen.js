/**
 * VendorBookingsScreen
 * Incoming bookings for vendor with approve/reject actions and reject reason modal
 */

import React, { useEffect, useCallback, useState } from 'react';
import { EventBus } from '../../utils/EventBus';
import {
    View, Text, FlatList, TouchableOpacity, Alert, StyleSheet,
    RefreshControl, Modal, TextInput, KeyboardAvoidingView, Platform, PanResponder
} from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getVendorBookingsThunk, approveBookingThunk, rejectBookingThunk, getPendingCountThunk, approveExtensionThunk, rejectExtensionThunk } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import Button from '../../components/Common/Button';
import EmptyState from '../../components/Common/EmptyState';
import { BookingCardSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { BookingStatus } from '../../utils/constants';
import chatService from '../../services/chat/chatService';

const FILTERS = [
    { label: 'All', value: null },
    { label: 'Pending', value: BookingStatus.Pending },
    { label: 'Active', value: BookingStatus.Confirmed },
    { label: 'Completed', value: BookingStatus.Completed },
    { label: 'Rejected', value: BookingStatus.Rejected },
];

const VendorBookingsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { vendorBookings, vendorBookingsLoading } = useSelector((s) => s.booking);
    const [activeFilter, setActiveFilter] = useState(0);
    const [refreshing, setRefreshing] = useState(false);
    const [rejectModalVisible, setRejectModalVisible] = useState(false);
    const [rejectBookingId, setRejectBookingId] = useState(null);
    const [rejectReason, setRejectReason] = useState('');
    const [rejectType, setRejectType] = useState('booking'); // 'booking' | 'extension'
    const [actionLoading, setActionLoading] = useState(null);

    useEffect(() => {
        dispatch(getVendorBookingsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getVendorBookingsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleApprove = useCallback((id) => {
        Alert.alert('Approve Booking', 'Confirm approval?', [
            { text: 'Cancel', style: 'cancel' },
            {
                text: 'Approve',
                onPress: async () => {
                    setActionLoading(id);
                    try {
                        const result = await dispatch(approveBookingThunk(id)).unwrap();
                        EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Booking approved successfully!', type: 'success' });
                        dispatch(getPendingCountThunk());
                    } catch (error) {
                        EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to approve booking' });
                    } finally {
                        setActionLoading(null);
                    }
                },
            },
        ]);
    }, [dispatch]);

    const openRejectModal = useCallback((id, type = 'booking') => {
        setRejectType(type);
        setRejectBookingId(id);
        setRejectReason('');
        setRejectModalVisible(true);
    }, []);

    const handleReject = useCallback(async () => {
        if (!rejectBookingId) return;
        setActionLoading(rejectBookingId);
        try {
            await dispatch(rejectBookingThunk({
                id: rejectBookingId,
                reason: rejectReason.trim() || 'Rejected by vendor',
            })).unwrap();
            EventBus.emit('SHOW_BANNER', { title: 'Done', message: 'Booking has been rejected.', type: 'success' });
            dispatch(getPendingCountThunk());
            setRejectModalVisible(false);
            setRejectBookingId(null);
            setRejectReason('');
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to reject booking' });
        } finally {
            setActionLoading(null);
        }
    }, [dispatch, rejectBookingId, rejectReason]);

    const handleApproveExtension = useCallback((id) => {
        Alert.alert('Approve Extension', 'Confirm approval?', [
            { text: 'Cancel', style: 'cancel' },
            {
                text: 'Approve',
                onPress: async () => {
                    setActionLoading(id);
                    try {
                        await dispatch(approveExtensionThunk(id)).unwrap();
                        EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Extension approved!', type: 'success' });
                    } catch (error) {
                        EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to approve extension' });
                    } finally {
                        setActionLoading(null);
                    }
                },
            },
        ]);
    }, [dispatch]);

    const handleRejectExtension = useCallback(async () => {
        if (!rejectBookingId) return;
        setActionLoading(rejectBookingId);
        try {
            await dispatch(rejectExtensionThunk({ id: rejectBookingId, reason: rejectReason.trim() || 'Rejected by vendor' })).unwrap();
            EventBus.emit('SHOW_BANNER', { title: 'Success', message: 'Extension rejected.', type: 'success' });
            dispatch(getPendingCountThunk());
            setRejectModalVisible(false);
            setRejectBookingId(null);
            setRejectReason('');
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: error || 'Failed to reject extension' });
        } finally {
            setActionLoading(null);
        }
    }, [dispatch, rejectBookingId, rejectReason]);

    const handleChatWithUser = useCallback(async (booking) => {
        try {
            const existing = await chatService.findConversationByParkingSpace(booking.parkingSpaceId);
            if (existing) {
                navigation.navigate('ChatScreen', {
                    conversationId: existing.id,
                    parkingSpaceId: booking.parkingSpaceId,
                    participantName: booking.userName,
                    parkingTitle: booking.parkingSpaceTitle,
                });
            } else {
                navigation.navigate('ChatScreen', {
                    conversationId: null,
                    parkingSpaceId: booking.parkingSpaceId,
                    participantName: booking.userName,
                    parkingTitle: booking.parkingSpaceTitle,
                });
            }
        } catch {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Could not open chat.' });
        }
    }, [navigation]);

    const filteredBookings = vendorBookings.filter((b) => {
        const filter = FILTERS[activeFilter].value;
        if (filter == null) return true;
        return b.status === filter;
    });

    const swipeResponder = React.useMemo(() => PanResponder.create({
        onMoveShouldSetPanResponder: (_, gestureState) => (
            Math.abs(gestureState.dx) > 18 && Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.25
        ),
        onPanResponderRelease: (_, gestureState) => {
            if (gestureState.dx > 80) {
                navigation.replace('MyBookings');
            }
        },
    }), [navigation]);

    const renderBooking = ({ item }) => (
        <Card onPress={() => navigation.navigate('BookingDetail', { bookingId: item.id })}>
            <View style={styles.cardHeader}>
                <View style={{ flex: 1 }}>
                    <Text style={styles.bookingTitle}>{item.userName}</Text>
                    <Text style={styles.parkingName}>{item.parkingSpaceTitle}</Text>
                </View>
                <View style={{ alignItems: 'flex-end', gap: 4 }}>
                    <Badge status={item.status} />
                    {item.hasPendingExtension && (
                        <Text style={styles.extensionBadge}>Ext. Pending</Text>
                    )}
                </View>
            </View>

            <View style={styles.detailRow}>
                <View style={styles.detailItem}>
                    <Ionicons name="calendar-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.detailText}>{formatDate(item.startDateTime)}</Text>
                </View>
                <View style={styles.detailItem}>
                    <Ionicons name="time-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.detailText}>{formatTime(item.startDateTime)} - {formatTime(item.endDateTime)}</Text>
                </View>
                <Text style={styles.amount}>{formatCurrency(item.totalAmount)}</Text>
            </View>

            {/* Actions for pending bookings */}
            {item.status === BookingStatus.Pending && (
                <View style={styles.actionRow}>
                    <Button
                        title="Approve"
                        onPress={() => handleApprove(item.id)}
                        size="sm"
                        style={{ flex: 1 }}
                        loading={actionLoading === item.id}
                        icon={<Ionicons name="checkmark" size={18} color={colors.white} />}
                    />
                    <Button
                        title="Reject"
                        onPress={() => openRejectModal(item.id)}
                        size="sm"
                        variant="danger"
                        style={{ flex: 1 }}
                        icon={<Ionicons name="close" size={18} color={colors.white} />}
                    />
                    <TouchableOpacity
                        style={styles.chatIconBtn}
                        onPress={() => handleChatWithUser(item)}
                    >
                        <Ionicons name="chatbubble-ellipses-outline" size={20} color={colors.primary} />
                    </TouchableOpacity>
                </View>
            )}

            {item.status !== BookingStatus.Pending && (
                <View style={styles.actionRow}>
                    <TouchableOpacity
                        style={styles.chatBtn}
                        onPress={() => handleChatWithUser(item)}
                    >
                        <Ionicons name="chatbubble-ellipses-outline" size={18} color={colors.primary} />
                        <Text style={styles.chatBtnText}>Chat with User</Text>
                    </TouchableOpacity>
                    {item.hasPendingExtension && (
                        <>
                            <TouchableOpacity
                                style={styles.extApproveBtn}
                                onPress={() => handleApproveExtension(item.id)}
                            >
                                <Ionicons name="checkmark" size={18} color={colors.white} />
                                <Text style={styles.extBtnText}>Approve Ext.</Text>
                            </TouchableOpacity>
                            <TouchableOpacity
                                style={styles.extRejectBtn}
                                onPress={() => openRejectModal(item.id, 'extension')}
                            >
                                <Ionicons name="close" size={18} color={colors.danger} />
                            </TouchableOpacity>
                        </>
                    )}
                </View>
            )}
        </Card>
    );

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <View style={styles.topTabRow}>
                    <TouchableOpacity
                        style={styles.topTab}
                        onPress={() => navigation.replace('MyBookings')}
                    >
                        <Text style={styles.topTabText}>Bookings</Text>
                    </TouchableOpacity>
                    <TouchableOpacity style={[styles.topTab, styles.topTabActive]}>
                        <Text style={[styles.topTabText, styles.topTabTextActive]}>Incoming</Text>
                    </TouchableOpacity>
                </View>
            </View>

            {/* Filters */}
            <View style={styles.filterRow}>
                {FILTERS.map((filter, idx) => (
                    <TouchableOpacity
                        key={idx}
                        onPress={() => setActiveFilter(idx)}
                        style={[styles.filterTab, activeFilter === idx && styles.filterTabActive]}
                    >
                        <Text style={[styles.filterTabText, activeFilter === idx && styles.filterTabTextActive]}>{filter.label}</Text>
                    </TouchableOpacity>
                ))}
            </View>

            {vendorBookingsLoading && !refreshing ? (
                <BookingCardSkeleton />
            ) : (
                <View style={styles.screenBody} {...swipeResponder.panHandlers}>
                    <FlatList
                        data={filteredBookings}
                        keyExtractor={(item) => item.id}
                        renderItem={renderBooking}
                        contentContainerStyle={styles.listContent}
                        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                        showsVerticalScrollIndicator={false}
                        ListEmptyComponent={<EmptyState icon="calendar-outline" title="No bookings" message="Bookings for your parking spaces will show here" />}
                    />
                </View>
            )}

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
                            Please provide a reason for rejection (optional):
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
                                title={actionLoading === rejectBookingId ? 'Rejecting...' : 'Confirm Reject'}
                                onPress={rejectType === 'booking' ? handleReject : handleRejectExtension}
                                variant="danger"
                                loading={actionLoading === rejectBookingId}
                                style={{ flex: 1 }}
                            />
                            <Button
                                title="Cancel"
                                onPress={() => {
                                    setRejectModalVisible(false);
                                    setRejectBookingId(null);
                                    setRejectReason('');
                                }}
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
    screenBody: { flex: 1 },
    header: { paddingTop: spacing.sm, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.sm },
    topTabRow: {
        flexDirection: 'row',
        padding: 4,
        borderRadius: 16,
        backgroundColor: colors.borderLight,
        marginBottom: spacing.sm,
    },
    topTab: {
        flex: 1,
        alignItems: 'center',
        justifyContent: 'center',
        paddingHorizontal: spacing.base,
        paddingVertical: 10,
        borderRadius: 12,
    },
    topTabActive: {
        backgroundColor: colors.surface,
        ...shadows.sm,
    },
    topTabText: { ...typography.caption, color: colors.textSecondary, fontWeight: '700' },
    topTabTextActive: { color: colors.textPrimary },
    filterRow: { flexDirection: 'row', paddingHorizontal: spacing.screenHorizontal, gap: spacing.sm, marginBottom: spacing.md },
    filterTab: { paddingHorizontal: spacing.base, paddingVertical: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
    filterTabActive: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
    filterTabText: { ...typography.caption, color: colors.textSecondary, fontWeight: '500' },
    filterTabTextActive: { color: colors.primary, fontWeight: '600' },
    listContent: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['2xl'] },
    cardHeader: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: spacing.sm },
    bookingTitle: { ...typography.label, color: colors.textPrimary },
    parkingName: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    detailRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
    detailItem: { flexDirection: 'row', alignItems: 'center', gap: 4 },
    detailText: { ...typography.caption, color: colors.textSecondary },
    amount: { ...typography.label, color: colors.primary, marginLeft: 'auto' },
    extensionBadge: { ...typography.caption, color: colors.warningDark, fontWeight: '700', marginTop: 2, paddingHorizontal: 6, paddingVertical: 2, backgroundColor: colors.warningSoft, borderRadius: 4, overflow: 'hidden' },
    actionRow: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.md, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.borderLight, alignItems: 'center' },
    chatIconBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.primarySoft, justifyContent: 'center', alignItems: 'center' },
    chatBtn: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.sm, paddingHorizontal: spacing.base, borderRadius: spacing.radius.full, backgroundColor: colors.primarySoft },
    chatBtnText: { ...typography.caption, color: colors.primary, fontWeight: '600' },
    extApproveBtn: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingVertical: spacing.sm, paddingHorizontal: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.success },
    extBtnText: { ...typography.caption, color: colors.white, fontWeight: '600' },
    extRejectBtn: { width: 36, height: 36, borderRadius: 18, backgroundColor: colors.dangerSoft, justifyContent: 'center', alignItems: 'center' },
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

export default VendorBookingsScreen;
