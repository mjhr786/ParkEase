/**
 * Root Navigator
 * Auth-conditional: shows Auth stack or App (role-based tabs)
 */

import React, { useEffect } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useDispatch } from 'react-redux';
import { useAuth } from '../hooks/useAuth';
import { restoreSessionThunk } from '../store/slices/authSlice';
import LoadingScreen from '../components/Common/LoadingScreen';
import AuthNavigator from './AuthNavigator';
import MemberTabNavigator from './MemberTabNavigator';
import VendorTabNavigator from './VendorTabNavigator';

const Stack = createNativeStackNavigator();

const RootNavigator = () => {
    const dispatch = useDispatch();
    const { isAuthenticated, isSessionChecked, isVendor, loading } = useAuth();

    useEffect(() => {
        dispatch(restoreSessionThunk());
    }, [dispatch]);

    if (!isSessionChecked || loading) {
        return <LoadingScreen message="Starting ParkEase..." />;
    }

    return (
        <NavigationContainer>
            <Stack.Navigator screenOptions={{ headerShown: false }}>
                {isAuthenticated ? (
                    isVendor ? (
                        <Stack.Screen name="VendorApp" component={VendorTabNavigator} />
                    ) : (
                        <Stack.Screen name="MemberApp" component={MemberTabNavigator} />
                    )
                ) : (
                    <Stack.Screen name="Auth" component={AuthNavigator} />
                )}
            </Stack.Navigator>
        </NavigationContainer>
    );
};

export default RootNavigator;
