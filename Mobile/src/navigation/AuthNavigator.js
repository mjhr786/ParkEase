/**
 * Auth Navigator
 * Onboarding → Login → Signup stack
 */

import React, { useState, useEffect } from 'react';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import LoginScreen from '../screens/Auth/LoginScreen';
import SignupScreen from '../screens/Auth/SignupScreen';
import OnboardingScreen from '../screens/Auth/OnboardingScreen';
import cacheService from '../services/storage/cacheService';

const Stack = createNativeStackNavigator();

const AuthNavigator = () => {
    const [checking, setChecking] = useState(true);
    const [onboardingDone, setOnboardingDone] = useState(true);

    useEffect(() => {
        (async () => {
            const done = await cacheService.isOnboardingDone();
            setOnboardingDone(done);
            setChecking(false);
        })();
    }, []);

    if (checking) return null;

    return (
        <Stack.Navigator
            initialRouteName={onboardingDone ? 'Login' : 'Onboarding'}
            screenOptions={{
                headerShown: false,
                animation: 'slide_from_right',
            }}
        >
            <Stack.Screen name="Onboarding" component={OnboardingScreen} />
            <Stack.Screen name="Login" component={LoginScreen} />
            <Stack.Screen name="Signup" component={SignupScreen} />
        </Stack.Navigator>
    );
};

export default AuthNavigator;
