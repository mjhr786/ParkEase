/**
 * App.js - ParkEase Mobile App Entry Point
 * Wraps the app with providers: Redux, SafeArea, Navigation, StatusBar
 */

import React from 'react';
import { StatusBar } from 'expo-status-bar';
import { Provider } from 'react-redux';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import store from './src/store';
import RootNavigator from './src/navigation/RootNavigator';
import GlobalErrorBanner from './src/components/Common/GlobalErrorBanner';
import NotificationService from './src/services/notifications/NotificationService';

export default function App() {
  React.useEffect(() => {
    NotificationService.initialize();
    return () => NotificationService.cleanup();
  }, []);

  return (
    <Provider store={store}>
      <SafeAreaProvider>
        <StatusBar style="auto" />
        <RootNavigator />
        <GlobalErrorBanner />
      </SafeAreaProvider>
    </Provider>
  );
}
