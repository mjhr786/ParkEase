/**
 * App.js - ParkEase Mobile App Entry Point
 * Wraps the app with providers: Redux, SafeArea, Navigation, StatusBar
 */

import React from 'react';
import { StatusBar } from 'expo-status-bar';
import { Provider } from 'react-redux';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { Modal, Platform, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import NetworkLogger, { startNetworkLogging, stopNetworkLogging } from 'react-native-network-logger';
import store from './src/store';
import RootNavigator from './src/navigation/RootNavigator';
import GlobalErrorBanner from './src/components/Common/GlobalErrorBanner';
import NotificationService from './src/services/notifications/NotificationService';
import RemoteConfigService from './src/services/remoteConfig/RemoteConfigService';

export default function App() {
  const [isNetworkLoggerVisible, setIsNetworkLoggerVisible] = React.useState(false);
  const [isDebuggerEnabled, setIsDebuggerEnabled] = React.useState(false);

  React.useEffect(() => {
    let isMounted = true;

    const initializeApp = async () => {
      await RemoteConfigService.initialize();

      const shouldEnableDebugger = await RemoteConfigService.getBooleanAsync('isDebuggerEnabled');
      if (isMounted) {
        setIsDebuggerEnabled(shouldEnableDebugger);
      }

      if (shouldEnableDebugger) {
        startNetworkLogging({
          ignoredPatterns: [/^GET https:\/\/firebaseremoteconfig\.googleapis\.com\//],
        });
      }
    };

    initializeApp();

    if (Platform.OS === 'android') {
      NotificationService.initialize();
    }

    return () => {
      isMounted = false;

      if (Platform.OS === 'android') {
        NotificationService.cleanup();
      }

      stopNetworkLogging();
    };
  }, []);

  return (
    <Provider store={store}>
      <SafeAreaProvider>
        <StatusBar style="auto" />
        <View style={styles.container}>
          <RootNavigator />
          <GlobalErrorBanner />

          {isDebuggerEnabled ? (
            <>
              <TouchableOpacity
                activeOpacity={0.9}
                onPress={() => setIsNetworkLoggerVisible(true)}
                style={styles.debugFab}
              >
                <Ionicons name="pulse-outline" size={18} color="#FFFFFF" />
                <Text style={styles.debugFabText}>Network</Text>
              </TouchableOpacity>

              <Modal
                visible={isNetworkLoggerVisible}
                animationType="slide"
                presentationStyle="fullScreen"
                onRequestClose={() => setIsNetworkLoggerVisible(false)}
              >
                <View style={styles.loggerHeader}>
                  <Text style={styles.loggerTitle}>Network Logger</Text>
                  <TouchableOpacity
                    activeOpacity={0.8}
                    onPress={() => setIsNetworkLoggerVisible(false)}
                    style={styles.closeButton}
                  >
                    <Ionicons name="close" size={22} color="#0A1828" />
                  </TouchableOpacity>
                </View>
                <View style={styles.loggerBody}>
                  <NetworkLogger theme="light" compact />
                </View>
              </Modal>
            </>
          ) : null}
        </View>
      </SafeAreaProvider>
    </Provider>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  debugFab: {
    position: 'absolute',
    right: 16,
    bottom: 24,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: '#0A1828',
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 10,
    shadowColor: '#0A1828',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.18,
    shadowRadius: 18,
    elevation: 8,
  },
  debugFabText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  loggerHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingTop: 18,
    paddingBottom: 12,
    backgroundColor: '#F5F7FA',
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#D5DEE8',
  },
  loggerTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#0A1828',
  },
  closeButton: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#FFFFFF',
  },
  loggerBody: {
    flex: 1,
  },
});
