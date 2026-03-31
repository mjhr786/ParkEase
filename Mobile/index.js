import { registerRootComponent } from 'expo';
import { Platform } from 'react-native';
import messaging from '@react-native-firebase/messaging';

import App from './App';

// Register background message handler (Android only)
if (Platform.OS === 'android') {
  messaging().setBackgroundMessageHandler(async remoteMessage => {
    console.log('[NotificationService] Message handled in the background!', remoteMessage);
  });
}

// registerRootComponent calls AppRegistry.registerComponent('main', () => App);
// It also ensures that whether you load the app in Expo Go or in a native build,
// the environment is set up appropriately
registerRootComponent(App);
