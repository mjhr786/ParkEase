/**
 * Root Navigator
 * Auth-conditional: shows Auth stack or unified App tabs
 */

import React, { useEffect } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useDispatch } from 'react-redux';
import { useAuth } from '../hooks/useAuth';
import { restoreSessionThunk, sessionExpired } from '../store/slices/authSlice';
import LoadingScreen from '../components/Common/LoadingScreen';
import AuthNavigator from './AuthNavigator';
import AppTabNavigator from './AppTabNavigator';
import { EventBus } from '../utils/EventBus';
import { AUTH_SESSION_EXPIRED_EVENT } from '../services/api/apiClient';

const Stack = createNativeStackNavigator();

const RootNavigator = () => {
    const dispatch = useDispatch();
    const { isAuthenticated, isSessionChecked, loading } = useAuth();

    useEffect(() => {
        dispatch(restoreSessionThunk());
    }, [dispatch]);

    useEffect(() => {
        const handleSessionExpired = () => {
            dispatch(sessionExpired());
        };

        EventBus.on(AUTH_SESSION_EXPIRED_EVENT, handleSessionExpired);

        return () => EventBus.off(AUTH_SESSION_EXPIRED_EVENT, handleSessionExpired);
    }, [dispatch]);

    if (!isSessionChecked || loading) {
        return <LoadingScreen message="Starting ParkEase..." />;
    }

    return (
        <NavigationContainer>
            <Stack.Navigator screenOptions={{ headerShown: false }}>
                {isAuthenticated ? (
                    <Stack.Screen name="App" component={AppTabNavigator} />
                ) : (
                    <Stack.Screen name="Auth" component={AuthNavigator} />
                )}
            </Stack.Navigator>
        </NavigationContainer>
    );
};

export default RootNavigator;
