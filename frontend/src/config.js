// Production co-hosts SPA + API on the same MonsterASP site → use relative URLs
// (avoids CORS). Set VITE_API_URL only when the API is on a different host.
export const API_BASE_URL =
  import.meta.env.VITE_API_URL ||
  (import.meta.env.PROD ? '' : 'http://localhost:5129');

export const API_ENDPOINTS = {
  BASE: `${API_BASE_URL}/api`,
  UPLOADS: `${API_BASE_URL}/uploads`,
  HUBS: `${API_BASE_URL}/hubs`,
};
