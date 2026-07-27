import React, { useState, useEffect, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, RefreshControl, SafeAreaView, Alert, TextInput } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../../styles/globalStyles';
import vehicleService from '../../services/api/vehicleService';

const VehiclesScreen = ({ navigation }) => {
    const [vehicles, setVehicles] = useState([]);
    const [loading, setLoading] = useState(false);
    
    // For simple inline adding
    const [isAdding, setIsAdding] = useState(false);
    const [newMake, setNewMake] = useState('');
    const [newModel, setNewModel] = useState('');
    const [newPlate, setNewPlate] = useState('');

    const fetchVehicles = useCallback(async () => {
        setLoading(true);
        try {
            const response = await vehicleService.getVehicles();
            if (response.success) {
                setVehicles(response.data || []);
            }
        } catch (error) {
            console.error('Failed to fetch vehicles', error);
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        fetchVehicles();
    }, [fetchVehicles]);

    const handleAddVehicle = async () => {
        if (!newMake || !newModel || !newPlate) {
            Alert.alert('Error', 'Please fill in all fields');
            return;
        }

        try {
            setLoading(true);
            const response = await vehicleService.addVehicle({
                make: newMake,
                model: newModel,
                licensePlate: newPlate,
                color: 'Unknown',
                type: 0,
                isDefault: vehicles.length === 0
            });
            
            if (response.success) {
                setIsAdding(false);
                setNewMake('');
                setNewModel('');
                setNewPlate('');
                fetchVehicles();
            }
        } catch (error) {
            Alert.alert('Error', 'Failed to add vehicle');
            setLoading(false);
        }
    };

    const handleDelete = async (id) => {
        Alert.alert('Delete Vehicle', 'Are you sure you want to remove this vehicle?', [
            { text: 'Cancel', style: 'cancel' },
            { 
                text: 'Delete', 
                style: 'destructive',
                onPress: async () => {
                    try {
                        setLoading(true);
                        await vehicleService.deleteVehicle(id);
                        fetchVehicles();
                    } catch (error) {
                        Alert.alert('Error', 'Failed to delete vehicle');
                        setLoading(false);
                    }
                }
            }
        ]);
    };

    const renderItem = ({ item }) => (
        <View style={styles.card}>
            <View style={styles.cardHeader}>
                <View style={styles.plateContainer}>
                    <Text style={styles.plateText}>{item.licensePlate}</Text>
                </View>
                {item.isDefault && (
                    <View style={styles.defaultBadge}>
                        <Text style={styles.defaultText}>Default</Text>
                    </View>
                )}
            </View>
            <View style={styles.cardFooter}>
                <Text style={styles.vehicleName}>{item.make} {item.model}</Text>
                <TouchableOpacity onPress={() => handleDelete(item.id)} style={styles.deleteButton}>
                    <Ionicons name="trash-outline" size={20} color={colors.error} />
                </TouchableOpacity>
            </View>
        </View>
    );

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>My Vehicles</Text>
                <TouchableOpacity onPress={() => setIsAdding(!isAdding)} style={styles.addButton}>
                    <Ionicons name={isAdding ? "close" : "add"} size={24} color={colors.primary} />
                </TouchableOpacity>
            </View>

            {isAdding && (
                <View style={styles.addForm}>
                    <Text style={styles.formTitle}>Add New Vehicle</Text>
                    <TextInput
                        style={styles.input}
                        placeholder="Make (e.g. Toyota)"
                        value={newMake}
                        onChangeText={setNewMake}
                        placeholderTextColor={colors.textTertiary}
                    />
                    <TextInput
                        style={styles.input}
                        placeholder="Model (e.g. Camry)"
                        value={newModel}
                        onChangeText={setNewModel}
                        placeholderTextColor={colors.textTertiary}
                    />
                    <TextInput
                        style={styles.input}
                        placeholder="License Plate"
                        value={newPlate}
                        onChangeText={setNewPlate}
                        autoCapitalize="characters"
                        placeholderTextColor={colors.textTertiary}
                    />
                    <TouchableOpacity style={styles.submitButton} onPress={handleAddVehicle}>
                        <Text style={styles.submitButtonText}>Save Vehicle</Text>
                    </TouchableOpacity>
                </View>
            )}

            <FlatList
                data={vehicles}
                keyExtractor={(item) => item.id.toString()}
                renderItem={renderItem}
                refreshControl={
                    <RefreshControl 
                        refreshing={loading && !isAdding} 
                        onRefresh={fetchVehicles} 
                        tintColor={colors.primary}
                    />
                }
                contentContainerStyle={styles.listContainer}
                ListEmptyComponent={
                    !isAdding && (
                        <View style={styles.emptyContainer}>
                            <Ionicons name="car-sport-outline" size={64} color={colors.borderLight} />
                            <Text style={styles.emptyText}>You haven't added any vehicles yet.</Text>
                            <TouchableOpacity style={styles.exploreButton} onPress={() => setIsAdding(true)}>
                                <Text style={styles.exploreButtonText}>Add a Vehicle</Text>
                            </TouchableOpacity>
                        </View>
                    )
                }
            />
        </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.background,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingHorizontal: 20,
        paddingVertical: 15,
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    backButton: { padding: 5 },
    addButton: { padding: 5 },
    headerTitle: {
        ...typography.h3,
        color: colors.text,
    },
    listContainer: {
        padding: 15,
        flexGrow: 1,
    },
    card: {
        backgroundColor: colors.surface,
        borderRadius: 12,
        padding: 15,
        marginBottom: 15,
        borderWidth: 1,
        borderColor: colors.borderLight,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.05,
        shadowRadius: 4,
        elevation: 2,
    },
    cardHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 15,
    },
    plateContainer: {
        backgroundColor: '#F3B700', // License plate yellow
        paddingHorizontal: 12,
        paddingVertical: 6,
        borderRadius: 6,
        borderWidth: 2,
        borderColor: '#333',
    },
    plateText: {
        ...typography.h4,
        color: '#111',
        fontWeight: 'bold',
        letterSpacing: 2,
    },
    defaultBadge: {
        backgroundColor: colors.primary + '20',
        paddingHorizontal: 10,
        paddingVertical: 4,
        borderRadius: 12,
    },
    defaultText: {
        ...typography.body3,
        color: colors.primary,
        fontWeight: '700',
    },
    cardFooter: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    vehicleName: {
        ...typography.subtitle1,
        color: colors.text,
    },
    deleteButton: {
        padding: 5,
    },
    addForm: {
        padding: 20,
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    formTitle: {
        ...typography.subtitle1,
        color: colors.text,
        marginBottom: 15,
    },
    input: {
        borderWidth: 1,
        borderColor: colors.borderLight,
        borderRadius: 8,
        padding: 12,
        marginBottom: 12,
        ...typography.body1,
        color: colors.text,
    },
    submitButton: {
        backgroundColor: colors.primary,
        padding: 15,
        borderRadius: 8,
        alignItems: 'center',
        marginTop: 5,
    },
    submitButtonText: {
        ...typography.button,
        color: '#FFF',
    },
    emptyContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        paddingVertical: 100,
    },
    emptyText: {
        ...typography.body1,
        color: colors.textTertiary,
        marginTop: 15,
        marginBottom: 25,
    },
    exploreButton: {
        backgroundColor: colors.primary,
        paddingHorizontal: 24,
        paddingVertical: 12,
        borderRadius: 25,
    },
    exploreButtonText: {
        ...typography.button,
        color: '#FFFFFF',
    }
});

export default VehiclesScreen;
