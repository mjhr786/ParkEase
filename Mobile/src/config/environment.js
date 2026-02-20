/**
 * Environment Configuration
 * Centralized config for API URLs and app settings
 */

const API_BASE = 'https://parkeaseapp.azurewebsites.net';

export const environment = {
  isDevelopment: __DEV__,
  isProduction: !__DEV__,
  apiUrl: `${API_BASE}/api`,
  uploadUrl: `${API_BASE}/uploads`,
  hubsUrl: `${API_BASE}/hubs`,
};

export default environment;
