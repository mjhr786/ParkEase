import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, FlatList, Switch } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { fetchCameraKeys, fetchPlateRules, toggleCameraKeyThunk } from '../../store/slices/iotSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography } from '../../styles/globalStyles';

const LprSettingsScreen = ({ route }) => {
    // Assuming parkingSpaceId is passed as parameter
    const { parkingSpaceId = 'ps_123' } = route?.params || {};
    const dispatch = useDispatch();
    const { cameraKeys, plateRules, isLoading } = useSelector(s => s.iot);

    useEffect(() => {
        dispatch(fetchCameraKeys(parkingSpaceId));
        dispatch(fetchPlateRules(parkingSpaceId));
    }, [dispatch, parkingSpaceId]);

    const handleToggleKey = (keyId, isEnabled) => {
        dispatch(toggleCameraKeyThunk({ parkingSpaceId, keyId, isEnabled }));
    };

    if (isLoading) return <LoadingScreen />;

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <Text style={styles.headerTitle}>LPR Settings</Text>
            </View>
            <View style={styles.content}>
                <Text style={styles.sectionTitle}>Camera Keys</Text>
                <FlatList
                    data={cameraKeys}
                    keyExtractor={item => item.id}
                    scrollEnabled={false}
                    renderItem={({ item }) => (
                        <Card style={styles.row}>
                            <View>
                                <Text style={styles.itemName}>{item.name}</Text>
                                <Text style={styles.itemSub}>ID: {item.id}</Text>
                            </View>
                            <Switch
                                value={item.isEnabled}
                                onValueChange={(val) => handleToggleKey(item.id, val)}
                                trackColor={{ true: colors.primary }}
                            />
                        </Card>
                    )}
                    ListEmptyComponent={<Text style={styles.emptyText}>No camera keys found.</Text>}
                />

                <Text style={[styles.sectionTitle, { marginTop: spacing.lg }]}>Plate Rules</Text>
                <FlatList
                    data={plateRules}
                    keyExtractor={item => item.id}
                    scrollEnabled={false}
                    renderItem={({ item }) => (
                        <Card style={styles.row}>
                            <View>
                                <Text style={styles.itemName}>{item.plateNumber}</Text>
                                <Text style={styles.itemSub}>{item.ruleType}</Text>
                            </View>
                            <Switch
                                value={item.isEnabled}
                                disabled
                            />
                        </Card>
                    )}
                    ListEmptyComponent={<Text style={styles.emptyText}>No plate rules found.</Text>}
                />
            </View>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { padding: spacing.lg, paddingTop: 60, backgroundColor: colors.surface },
    headerTitle: { ...typography.h3, color: colors.textPrimary },
    content: { padding: spacing.lg },
    sectionTitle: { ...typography.h4, color: colors.textPrimary, marginBottom: spacing.sm },
    row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.sm },
    itemName: { ...typography.body, fontWeight: '600', color: colors.textPrimary },
    itemSub: { ...typography.caption, color: colors.textTertiary },
    emptyText: { ...typography.body, color: colors.textSecondary, fontStyle: 'italic' }
});

export default LprSettingsScreen;
