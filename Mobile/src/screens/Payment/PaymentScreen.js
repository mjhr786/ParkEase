/**
 * PaymentScreen
 * Payment processing for a booking — shows summary, processes payment, displays result
 */

import React, { useEffect, useCallback, useState } from 'react';
import { EventBus } from '../../utils/EventBus';
import { View, Text, ScrollView, StyleSheet, Platform, Linking, Share, Image, TextInput } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import {
    processPaymentThunk,
    clearPaymentResult,
    getPaymentsConfigThunk,
    createUpiPaymentOrderThunk,
    verifyPaymentThunk,
} from '../../store/slices/paymentSlice';
import { updateProfileThunk } from '../../store/slices/authSlice';
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
    const { loading, paymentResult, error, paymentsConfig, upiOrder } = useSelector((state) => state.payment);
    const { user } = useSelector((state) => state.auth);
    const { bookingId, amount, parkingTitle, priceBreakdown } = route.params || {};
    const [selectedMethod, setSelectedMethod] = useState(PaymentMethod.CreditCard);
    const [phone, setPhone] = useState(user?.phoneNumber || '');
    const [upiSession, setUpiSession] = useState(null);

    const upiEnabled = !!paymentsConfig?.upiEnabled;
    const contactRequired = paymentsConfig?.upiContactRequired === true || !user?.phoneNumber;

    useEffect(() => {
        dispatch(getPaymentsConfigThunk());
        return () => {
            dispatch(clearPaymentResult());
        };
    }, [dispatch]);

    useEffect(() => {
        if (!upiSession) return undefined;
        const id = setInterval(async () => {
            const result = await dispatch(verifyPaymentThunk({
                bookingId,
                razorpayPaymentId: upiSession.providerPaymentId,
                razorpayOrderId: upiSession.providerOrderId,
                rail: 'upi',
            }));
            if (!result.error) {
                EventBus.emit('SHOW_BANNER', { title: 'Payment Successful', message: 'UPI payment captured.', type: 'success' });
                navigation.goBack();
            }
        }, 3000);
        return () => clearInterval(id);
    }, [dispatch, bookingId, upiSession, navigation]);

    const handlePay = useCallback(async () => {
        if (!bookingId || !amount) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Error', message: 'Missing booking information' });
            return;
        }

        if (upiEnabled && selectedMethod === PaymentMethod.UPI) {
            if (contactRequired) {
                EventBus.emit('SHOW_ERROR_BANNER', {
                    title: 'Phone required',
                    message: 'Add a 10-digit Indian mobile number to pay with UPI',
                });
                return;
            }
            const result = await dispatch(createUpiPaymentOrderThunk({ bookingId }));
            if (result.error) {
                EventBus.emit('SHOW_ERROR_BANNER', {
                    title: 'UPI payment failed',
                    message: result.payload?.message || result.payload || 'Please try again.',
                });
                return;
            }
            const session = result.payload;
            setUpiSession(session);
            if (Platform.OS === 'android' && session?.upiIntentUrl) {
                await Linking.openURL(session.upiIntentUrl);
            }
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
    }, [dispatch, bookingId, amount, selectedMethod, navigation, upiEnabled, contactRequired]);

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
                    if (methodKey === PaymentMethod.UPI && !upiEnabled) return null;
                    const isSelected = selectedMethod === methodKey;
                    const upiBlocked = methodKey === PaymentMethod.UPI && contactRequired;
                    const iconMap = {
                        [PaymentMethod.CreditCard]: 'card-outline',
                        [PaymentMethod.DebitCard]: 'card-outline',
                        [PaymentMethod.UPI]: 'phone-portrait-outline',
                        [PaymentMethod.NetBanking]: 'globe-outline',
                        [PaymentMethod.Wallet]: 'wallet-outline',
                    };
                    return (
                        <Button
                            key={key}
                            title={upiBlocked ? `${label} (add phone)` : label}
                            variant={isSelected ? 'primary' : 'ghost'}
                            onPress={() => setSelectedMethod(methodKey)}
                            icon={<Ionicons name={iconMap[methodKey] || 'card-outline'} size={18} color={isSelected ? colors.white : colors.textSecondary} />}
                            style={styles.methodBtn}
                        />
                    );
                })}
                {upiEnabled && selectedMethod === PaymentMethod.UPI && contactRequired && (
                    <View>
                        <Text style={styles.sectionTitle}>Indian mobile number</Text>
                        <TextInput
                            value={phone}
                            onChangeText={setPhone}
                            keyboardType="phone-pad"
                            placeholder="10-digit mobile"
                            style={styles.phoneInput}
                        />
                        <Button
                            title="Save phone"
                            onPress={async () => {
                                const result = await dispatch(updateProfileThunk({
                                    firstName: user?.firstName,
                                    lastName: user?.lastName,
                                    phoneNumber: phone,
                                }));
                                if (updateProfileThunk.fulfilled.match(result)) {
                                    dispatch(getPaymentsConfigThunk());
                                }
                            }}
                        />
                    </View>
                )}
            </Card>

            {upiSession?.upiIntentUrl && Platform.OS !== 'android' && (
                <Card>
                    <Text style={styles.sectionTitle}>Scan or copy the full UPI URI</Text>
                    <Image
                        source={{
                            uri: `https://api.qrserver.com/v1/create-qr-code/?size=280x280&data=${encodeURIComponent(upiSession.upiIntentUrl)}`,
                        }}
                        style={styles.qr}
                    />
                    <Text selectable style={styles.uri}>{upiSession.upiIntentUrl}</Text>
                    <Button
                        title="Share full UPI URI"
                        onPress={() => Share.share({ message: upiSession.upiIntentUrl })}
                    />
                </Card>
            )}

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
    phoneInput: {
        borderWidth: 1,
        borderColor: colors.borderLight,
        borderRadius: 8,
        padding: spacing.sm,
        marginBottom: spacing.sm,
        color: colors.textPrimary,
    },
    qr: { width: 280, height: 280, alignSelf: 'center', marginVertical: spacing.sm, backgroundColor: '#fff' },
    uri: { ...typography.caption, color: colors.textSecondary, marginBottom: spacing.sm },
    footer: {
        paddingHorizontal: spacing.screenHorizontal,
        paddingVertical: spacing.xl,
    },
});

export default PaymentScreen;
