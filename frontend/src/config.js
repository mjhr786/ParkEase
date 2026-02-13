export const API_BASE_URL = import.meta.env.VITE_API_URL || (import.meta.env.PROD ? 'https://parkeaseapp.azurewebsites.net' : 'http://localhost:5129');

export const API_ENDPOINTS = {
    BASE: `${API_BASE_URL}/api`,
    UPLOADS: `${API_BASE_URL}/uploads`,
    HUBS: `${API_BASE_URL}/hubs`
};
