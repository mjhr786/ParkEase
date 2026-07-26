import React, { useEffect, useState, useCallback } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, RefreshControl } from 'react-native';
import { useSelector, useDispatch } from 'react-redux';
import { useNavigation } from '@react-navigation/native';
import corporateService from '../../services/api/corporateService';
import { fetchMyCompanies } from '../../store/slices/corporateSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Button from '../../components/Common/Button';
import { globalStyles, colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { Ionicons } from '@expo/vector-icons';

const QuickAction = ({ icon, label, onPress }) => (
    <TouchableOpacity style={styles.actionButton} onPress={onPress}>
        <View style={styles.actionIconWrapper}>
            <Ionicons name={icon} size={24} color={colors.primary} />
        </View>
        <Text style={styles.actionLabel}>{label}</Text>
    </TouchableOpacity>
);

const MetricCard = ({ label, value, icon, color }) => (
    <View style={styles.metricCard}>
        <View style={[styles.metricIconWrapper, { backgroundColor: color + '20' }]}>
            <Ionicons name={icon} size={24} color={color} />
        </View>
        <View style={styles.metricInfo}>
            <Text style={styles.metricValue}>{value !== undefined ? value : '-'}</Text>
            <Text style={styles.metricLabel}>{label}</Text>
        </View>
    </View>
);

const CorporateDashboardScreen = () => {
    const dispatch = useDispatch();
    const navigation = useNavigation();
    const { myCompanies, activeCompanyId, isLoading: reduxLoading } = useSelector((state) => state.corporate);
    
    const [dashboardData, setDashboardData] = useState(null);
    const [isRefreshing, setIsRefreshing] = useState(false);

    const activeCompany = myCompanies.find(c => c.id === activeCompanyId);

    const loadDashboard = useCallback(async () => {
        if (!activeCompanyId) return;
        try {
            const data = await corporateService.getDashboard(activeCompanyId);
            setDashboardData(data);
        } catch (error) {
            console.error('Failed to load dashboard', error);
        }
    }, [activeCompanyId]);

    const onRefresh = async () => {
        setIsRefreshing(true);
        await dispatch(fetchMyCompanies());
        await loadDashboard();
        setIsRefreshing(false);
    };

    useEffect(() => {
        dispatch(fetchMyCompanies());
    }, [dispatch]);

    useEffect(() => {
        if (activeCompanyId) {
            loadDashboard();
        }
    }, [activeCompanyId, loadDashboard]);

    // Empty State: User doesn't belong to any company
    if (myCompanies.length === 0 && !reduxLoading) {
        return (
            <ScreenLayout>
                <View style={[globalStyles.center, { padding: spacing.xl }]}>
                    <Ionicons name="business" size={80} color={colors.primarySoft} />
                    <Text style={[typography.h2, { marginTop: spacing.lg, textAlign: 'center' }]}>
                        Welcome to Corporate
                    </Text>
                    <Text style={[typography.body, { textAlign: 'center', color: colors.textSecondary, marginTop: spacing.sm, marginBottom: spacing.xl }]}>
                        You are not part of any company yet. Create your first company to start managing bulk parking leases and employee access.
                    </Text>
                    <Button 
                        title="Create a Company" 
                        onPress={() => navigation.navigate('CompanyManagement')} 
                        style={{ width: '100%' }}
                    />
                </View>
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout scrollable={false} edges={['top']}>
            <ScrollView 
                contentContainerStyle={[
                    globalStyles.screenPadded, 
                    { flex: undefined, flexGrow: 1, paddingBottom: spacing['3xl'] }
                ]}
                refreshControl={<RefreshControl refreshing={isRefreshing || reduxLoading} onRefresh={onRefresh} />}
            >
                <View style={styles.header}>
                    <View>
                        <Text style={typography.caption}>ACTIVE COMPANY</Text>
                        <Text style={typography.h2}>{activeCompany?.name || 'Loading...'}</Text>
                    </View>
                    <TouchableOpacity onPress={() => navigation.navigate('CompanyManagement')} style={styles.switchBtn}>
                        <Ionicons name="swap-horizontal" size={20} color={colors.primary} />
                    </TouchableOpacity>
                </View>

                {/* Metrics */}
                <Card style={styles.metricsContainer}>
                    <Text style={[typography.h3, { marginBottom: spacing.md }]}>Overview</Text>
                    <View style={styles.metricsGrid}>
                        <MetricCard 
                            label="Members" 
                            value={dashboardData?.totalMembers} 
                            icon="people" 
                            color={colors.primary} 
                        />
                        <MetricCard 
                            label="Active Leases" 
                            value={dashboardData?.activeAllocations} 
                            icon="document-text" 
                            color={colors.secondary} 
                        />
                        <MetricCard 
                            label="Bookings Today" 
                            value={dashboardData?.todaysBookings} 
                            icon="calendar" 
                            color={colors.success} 
                        />
                    </View>
                </Card>

                {/* Quick Actions */}
                <Text style={[typography.h3, { marginVertical: spacing.md }]}>Quick Actions</Text>
                <View style={styles.actionsGrid}>
                    <QuickAction 
                        icon="people-outline" 
                        label="Members" 
                        onPress={() => navigation.navigate('CorporateMembers')} 
                    />
                    <QuickAction 
                        icon="key-outline" 
                        label="Allocations" 
                        onPress={() => navigation.navigate('CorporateAllocations')} 
                    />
                    <QuickAction 
                        icon="calendar-outline" 
                        label="Bookings" 
                        onPress={() => navigation.navigate('CorporateBookings')} 
                    />
                    <QuickAction 
                        icon="settings-outline" 
                        label="Settings" 
                        onPress={() => navigation.navigate('CompanyManagement')} 
                    />
                </View>

            </ScrollView>
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingVertical: spacing.xl,
    },
    switchBtn: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
    },
    metricsContainer: {
        marginBottom: spacing.xl,
    },
    metricsGrid: {
        gap: spacing.md,
    },
    metricCard: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.background,
        padding: spacing.md,
        borderRadius: spacing.radius.md,
    },
    metricIconWrapper: {
        width: 48,
        height: 48,
        borderRadius: 24,
        justifyContent: 'center',
        alignItems: 'center',
        marginRight: spacing.md,
    },
    metricInfo: {
        flex: 1,
    },
    metricValue: {
        ...typography.h2,
        color: colors.textPrimary,
    },
    metricLabel: {
        ...typography.caption,
        color: colors.textSecondary,
    },
    actionsGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        justifyContent: 'space-between',
        gap: spacing.md,
    },
    actionButton: {
        width: '47%',
        backgroundColor: colors.surface,
        padding: spacing.md,
        borderRadius: spacing.radius.lg,
        alignItems: 'center',
        ...shadows.sm,
    },
    actionIconWrapper: {
        width: 56,
        height: 56,
        borderRadius: 28,
        backgroundColor: colors.primarySoft,
        justifyContent: 'center',
        alignItems: 'center',
        marginBottom: spacing.sm,
    },
    actionLabel: {
        ...typography.bodySmall,
        fontWeight: '600',
        color: colors.textPrimary,
    }
});

export default CorporateDashboardScreen;
