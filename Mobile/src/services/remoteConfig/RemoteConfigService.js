import remoteConfig from '@react-native-firebase/remote-config';

class RemoteConfigService {
  async initialize() {
    try {
      console.log('[RemoteConfigService] Initializing remote config defaults...');
      
      // Set default values so the app works before cloud fetch
      await remoteConfig().setDefaults({
        isDisplayFCMTokenEnabled: false,
      });

      // 0 means fetch aggressively for development, 3600 (1 hour) for production
      // Adjust this as needed for your cache strategy.
      await remoteConfig().setConfigSettings({
        minimumFetchIntervalMillis: __DEV__ ? 0 : 3600000,
      });

      console.log('[RemoteConfigService] Fetching and activating configs...');
      const fetchedAndActivated = await remoteConfig().fetchAndActivate();
      
      if (fetchedAndActivated) {
        console.log('[RemoteConfigService] Successfully fetched and activated new configs!');
      } else {
        console.log('[RemoteConfigService] No new configs were fetched from the server.');
      }
    } catch (error) {
      console.error('[RemoteConfigService] Failed to initialize Remote Config:', error);
    }
  }

  getBoolean(key) {
    return remoteConfig().getValue(key).asBoolean();
  }

  getString(key) {
    return remoteConfig().getValue(key).asString();
  }

  getNumber(key) {
    return remoteConfig().getValue(key).asNumber();
  }
}

export default new RemoteConfigService();
