export default ({ config }) => {
  return {
    ...config,
    android: {
      ...config.android,
      googleServicesFile: process.env.GOOGLE_SERVICES_JSON ?? config.android.googleServicesFile,
    },
    ios: {
      ...config.ios,
      googleServicesFile: process.env.GOOGLE_SERVICES_PLIST ?? config.ios.googleServicesFile,
    }
  };
};
