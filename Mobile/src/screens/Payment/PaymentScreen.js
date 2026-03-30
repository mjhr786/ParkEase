/**
 * PaymentScreen
 * Payment processing for a booking — shows summary, processes payment, displays result
 */

import React, { useEffect, useCallback, useState } from 'react';
import { EventBus } from '../../utils/EventBus';
import { View, Text, ScrollView, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { processPaymentThunk, clearPaymentResult } from '../../store/slices/paymentSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatCurrency } from '../../utils/formatters';
import { PaymentMethodLabels, PaymentMethod } from '../../utils/constants';

const InfoRow = ({ label, value, bold }) => (
    <View style={rowStyles.row}>
        <Text style={rowStyles.label}>{label}</Text>
        <Text style={[rowStyles.value, bold && rowStyles.bold]}>{value}</Text>
    </View>
);

const rowStyles = StyleSheet.create({
    row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: spacing.sm },
    label: { ...typography.body, color: colors.textSecondary },
    value: { ...typography.body, color: colors.textPrimary },
    bold: { fontWeight: '700' },
});

const PaymentScreen = ({ navigation, route }) => {
    const dispatch = useDispatch();
    const { loading, paymentResult, error } = useSelector((state) => state.payment);
    const { bookingId, amount, parkingTitle, priceBreakdown } = route.params || {};
    const [selectedMethod, setSelectedMethod] = useState(PaymentMethod.CreditCard);

    useEffect(() => {
        return () => {
            dispatch(clearPaymentResult());
        };
    }, [dispatch]);

    const handlePay = useCallback(async () => {
        if (!bookingId || !amount) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Missing booking information' });
            return;
        }
        const result = await dispatch(processPaymentThunk({
            bookingId,
            amount,
            paymentMethod: selectedMethod,
        }));
        if (!result.error) {
            EventBus.emit('SHOW_BANNER', { title: 'Payment Successful', message: 'Your payment has been processed.', type: 'success' });
            navigation.goBack();
        } else {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Payment Failed', message: result.payload || 'Please try again.' });
        }
    }, [dispatch, bookingId, amount, selectedMethod, navigation]);

    return (
        <ScreenLayout scrollable>
            <View style={styles.header}>
                <Ionicons name="arrow-back" size={24} color={colors.textPrimary} onPress={() => navigation.goBack()} />
                <Text style={styles.title}>Payment</Text>
                <View style={{ width: 24 }} />
            </View>

            {/* Booking Summary */}
            <Card>
                <Text style={styles.sectionTitle}>Booking Summary</Text>
                {parkingTitle && <InfoRow label="Parking" value={parkingTitle} />}
                {priceBreakdown?.duration && <InfoRow label="Duration" value={priceBreakdown.duration} />}
                {priceBreakdown?.basePrice != null && (
                    <InfoRow label="Base Price" value={formatCurrency(priceBreakdown.basePrice)} />
                )}
                {priceBreakdown?.serviceFee != null && (
                    <InfoRow label="Service Fee" value={formatCurrency(priceBreakdown.serviceFee)} />
                )}
                <View style={styles.divider} />
                <InfoRow label="Total" value={formatCurrency(amount || 0)} bold />
            </Card>

            {/* Payment Method */}
            <Card>
                <Text style={styles.sectionTitle}>Payment Method</Text>
                {Object.entries(PaymentMethodLabels).map(([key, label]) => {
                    const methodKey = Number(key);
                    const isSelected = selectedMethod === methodKey;
                    const iconMap = {
                        [PaymentMethod.CreditCard]: 'card-outline',
                        [PaymentMethod.DebitCard]: 'card-outline',
                        [PaymentMethod.UPI]: 'phone-portrait-outline',
                        [PaymentMethod.NetBanking]: 'globe-outline',
                        [PaymentMethod.Wallet]: 'wallet-outline',
                        [PaymentMethod.Cash]: 'cash-outline',
                    };
                    return (
                        <Button
                            key={key}
                            title={label}
                            variant={isSelected ? 'primary' : 'ghost'}
                            onPress={() => setSelectedMethod(methodKey)}
                            icon={<Ionicons name={iconMap[methodKey] || 'card-outline'} size={18} color={isSelected ? colors.white : colors.textSecondary} />}
                            style={styles.methodBtn}
                        />
                    );
                })}
            </Card>

            {/* Pay Button */}
            <View style={styles.footer}>
                <Button
                    title={`Pay ${formatCurrency(amount || 0)}`}
                    onPress={handlePay}
                    loading={loading}
                    icon={<Ionicons name="lock-closed" size={18} color={colors.white} />}
                />
            </View>
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
    sectionTitle: { ...typography.label, color: colors.textPrimary, marginBottom: spacing.sm },
    divider: { height: 1, backgroundColor: colors.borderLight, marginVertical: spacing.sm },
    methodBtn: { marginBottom: spacing.xs },
    footer: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingVertical: spacing.xl,
    },
});

export default PaymentScreen;
