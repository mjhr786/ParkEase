import React, { useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { fetchOnSalePackages } from '../../store/slices/eventPackageSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { formatCurrency, formatDateTime } from '../../utils/formatters';

const EventPackagesScreen = () => {
    const dispatch = useDispatch();
    const { onSalePackages, isLoading } = useSelector(s => s.eventPackage);

    useEffect(() => {
        dispatch(fetchOnSalePackages());
    }, [dispatch]);

    if (isLoading) return <LoadingScreen />;

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <Text style={styles.headerTitle}>Event Packages</Text>
            </View>
            <FlatList
                data={onSalePackages}
                keyExtractor={item => item.id.toString()}
                contentContainerStyle={styles.list}
                renderItem={({ item }) => (
                    <Card style={styles.card}>
                        <Text style={styles.title}>{item.name}</Text>
                        <Text style={styles.eventInfo}>{item.venueName} - {item.eventName}</Text>
                        <Text style={styles.timeInfo}>{formatDateTime(item.eventStart)}</Text>
                        <View style={styles.footer}>
                            <Text style={styles.price}>{formatCurrency(item.price)}</Text>
                            <TouchableOpacity style={styles.buyBtn}>
                                <Text style={styles.buyBtnText}>Purchase</Text>
                            </TouchableOpacity>
                        </View>
                    </Card>
                )}
                ListEmptyComponent={<Text style={styles.emptyText}>No event packages on sale.</Text>}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { padding: spacing.lg, paddingTop: 60, backgroundColor: colors.surface },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    list: { padding: spacing.lg, gap: spacing.md },
    card: { gap: spacing.sm },
    title: { ...typography.h4, color: colors.textPrimary },
    eventInfo: { ...typography.body, color: colors.textSecondary },
    timeInfo: { ...typography.caption, color: colors.textTertiary },
    footer: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.sm },
    price: { ...typography.h4, color: colors.primary },
    buyBtn: { backgroundColor: colors.primary, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, borderRadius: 8 },
    buyBtnText: { ...typography.bodySmall, color: colors.white, fontWeight: 'bold' },
    emptyText: { ...typography.body, color: colors.textSecondary, textAlign: 'center', marginTop: spacing.xl }
});

export default EventPackagesScreen;
