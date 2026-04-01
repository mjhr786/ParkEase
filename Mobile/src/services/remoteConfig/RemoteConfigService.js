import remoteConfig from '@react-native-firebase/remote-config';

class RemoteConfigService {
  constructor() {
    this.initializePromise = null;
  }

  async initialize() {
    if (this.initializePromise) {
      return this.initializePromise;
    }

    this.initializePromise = (async () => {
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
        this.initializePromise = null;
      }
    })();

    return this.initializePromise;
  }

  async refresh() {
    await this.initialize();

    try {
      console.log('[RemoteConfigService] Force refreshing remote config...');
      await remoteConfig().fetch(0);
      await remoteConfig().activate();
    } catch (error) {
      console.error('[RemoteConfigService] Failed to refresh Remote Config:', error);
    }
  }

  async getBooleanAsync(key, options = {}) {
    const { refresh = false } = options;

    if (refresh) {
      await this.refresh();
    } else {
      await this.initialize();
    }

    return this.getBoolean(key);
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
