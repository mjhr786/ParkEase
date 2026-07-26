import React, { useEffect } from 'react';
import { View, Text, StyleSheet, FlatList } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { fetchMyServices } from '../../store/slices/ancillarySlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography } from '../../styles/globalStyles';

const AncillaryServicesScreen = () => {
    const dispatch = useDispatch();
    const { myServices, isLoading } = useSelector(s => s.ancillary);

    useEffect(() => {
        dispatch(fetchMyServices());
    }, [dispatch]);

    if (isLoading) return <LoadingScreen />;

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <Text style={styles.headerTitle}>Ancillary Services</Text>
            </View>
            <FlatList
                data={myServices}
                keyExtractor={item => item.id.toString()}
                contentContainerStyle={styles.list}
                renderItem={({ item }) => (
                    <Card style={styles.card}>
                        <Text style={styles.title}>{item.name}</Text>
                        <Text style={styles.desc}>{item.description}</Text>
                        <Text style={styles.price}>${item.price.toFixed(2)}</Text>
                    </Card>
                )}
                ListEmptyComponent={<Text style={styles.emptyText}>No ancillary services found.</Text>}
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
    desc: { ...typography.body, color: colors.textSecondary },
    price: { ...typography.h4, color: colors.primary },
    emptyText: { ...typography.body, color: colors.textSecondary, textAlign: 'center', marginTop: spacing.xl }
});

export default AncillaryServicesScreen;
