import React, { useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { getInvoicesThunk } from '../../store/slices/corporateSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import LoadingScreen from '../../components/Common/LoadingScreen';
import Badge from '../../components/Common/Badge';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { formatCurrency, formatDateTime } from '../../utils/formatters';

const CorporateInvoicesScreen = () => {
    const dispatch = useDispatch();
    const { invoices, isLoading, activeCompanyId } = useSelector(s => s.corporate);

    useEffect(() => {
        if (activeCompanyId) {
            dispatch(getInvoicesThunk({ companyId: activeCompanyId, params: {} }));
        }
    }, [dispatch, activeCompanyId]);

    if (isLoading) return <LoadingScreen />;

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <Text style={styles.headerTitle}>Corporate Invoices</Text>
            </View>
            {!activeCompanyId ? (
                <Text style={styles.emptyText}>Please select a company first.</Text>
            ) : (
                <FlatList
                    data={invoices}
                    keyExtractor={item => item.id}
                    contentContainerStyle={styles.list}
                    renderItem={({ item }) => (
                        <Card style={styles.card}>
                            <View style={styles.row}>
                                <Text style={styles.invoiceNumber}>Inv: {item.invoiceNumber}</Text>
                                <Badge status={item.status} />
                            </View>
                            <Text style={styles.dateText}>Due: {formatDateTime(item.dueDate)}</Text>
                            <Text style={styles.amount}>{formatCurrency(item.totalAmount)}</Text>
                            <TouchableOpacity style={styles.viewBtn}>
                                <Text style={styles.viewBtnText}>View Details</Text>
                            </TouchableOpacity>
                        </Card>
                    )}
                    ListEmptyComponent={<Text style={styles.emptyText}>No invoices found for this company.</Text>}
                />
            )}
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { padding: spacing.lg, paddingTop: 60, backgroundColor: colors.surface },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    list: { padding: spacing.lg, gap: spacing.md },
    card: { gap: spacing.sm },
    row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    invoiceNumber: { ...typography.body, fontWeight: 'bold', color: colors.textPrimary },
    dateText: { ...typography.caption, color: colors.textSecondary },
    amount: { ...typography.h4, color: colors.primary, marginVertical: spacing.xs },
    viewBtn: { alignSelf: 'flex-start' },
    viewBtnText: { ...typography.bodySmall, color: colors.primary, fontWeight: '600' },
    emptyText: { ...typography.body, color: colors.textSecondary, textAlign: 'center', marginTop: spacing.xl }
});

export default CorporateInvoicesScreen;
