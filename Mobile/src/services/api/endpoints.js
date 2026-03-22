/**
 * API Endpoints
 * All endpoint constants matching the backend controllers
 */

export const ENDPOINTS = {
    // Auth
    AUTH: {
        REGISTER: '/auth/register',
        LOGIN: '/auth/login',
        REFRESH: '/auth/refresh',
        LOGOUT: '/auth/logout',
        CHANGE_PASSWORD: '/auth/change-password',
    },

    // Users
    USERS: {
        ME: '/users/me',
    },

    // Parking
    PARKING: {
        BASE: '/parking',
        SEARCH: '/parking/search',
        MAP: '/parking/map',
        MY_LISTINGS: '/parking/my-listings',
        BY_ID: (id) => `/parking/${id}`,
        TOGGLE_ACTIVE: (id) => `/parking/${id}/toggle-active`,
    },

    // Bookings (V2)
    BOOKINGS: {
        BASE: '/v2/bookings',
        MY_BOOKINGS: '/v2/bookings/my-bookings',
        VENDOR_BOOKINGS: '/v2/bookings/vendor-bookings',
        PENDING_COUNT: '/v2/bookings/pending-count',
        CALCULATE_PRICE: '/v2/bookings/calculate-price',
        BY_ID: (id) => `/v2/bookings/${id}`,
        BY_REFERENCE: (ref) => `/v2/bookings/reference/${ref}`,
        CANCEL: (id) => `/v2/bookings/${id}/cancel`,
        APPROVE: (id) => `/v2/bookings/${id}/approve`,
        REJECT: (id) => `/v2/bookings/${id}/reject`,
        CHECK_IN: (id) => `/v2/bookings/${id}/check-in`,
        CHECK_OUT: (id) => `/v2/bookings/${id}/check-out`,
        EXTEND: (id) => `/v2/bookings/${id}/extend`,
        APPROVE_EXTENSION: (id) => `/v2/bookings/${id}/approve-extension`,
        REJECT_EXTENSION: (id) => `/v2/bookings/${id}/reject-extension`,
    },

    // Payments
    PAYMENTS: {
        BASE: '/payments',
        STRIPE_CONFIG: '/payments/stripe-config',
        VERIFY: '/payments/verify',
        REFUND: '/payments/refund',
        BY_ID: (id) => `/payments/${id}`,
    },

    // Chat
    CHAT: {
        CONVERSATIONS: '/chat/conversations',
        MESSAGES: (id) => `/chat/conversations/${id}/messages`,
        SEND: '/chat/send',
        UNREAD_COUNT: '/chat/unread-count',
    },

    // Notifications
    NOTIFICATIONS: {
        BASE: '/notifications',
        MARK_READ: (id) => `/notifications/${id}/read`,
        DELETE: (id) => `/notifications/${id}`,
    },

    // Vehicles
    VEHICLES: {
        BASE: '/vehicles',
    },

    // Favorites
    FAVORITES: {
        BASE: '/favorites',
        TOGGLE: (id) => `/favorites/${id}/toggle`,
    },

    // Reviews
    REVIEWS: {
        BASE: '/reviews',
        BY_ID: (id) => `/reviews/${id}`,
        BY_PARKING_SPACE: (parkingSpaceId) => `/reviews/parking-space/${parkingSpaceId}`,
        OWNER_RESPONSE: (id) => `/reviews/${id}/owner-response`,
    },

    // Dashboard
    DASHBOARD: {
        VENDOR: '/dashboard/vendor',
        MEMBER: '/dashboard/member',
    },

    // Files
    FILES: {
        UPLOAD: (parkingSpaceId) => `/files/parking/${parkingSpaceId}/upload`,
        DELETE: (parkingSpaceId, fileName) => `/files/parking/${parkingSpaceId}/${fileName}`,
        GET: (parkingSpaceId) => `/files/parking/${parkingSpaceId}`,
    },
};

export default ENDPOINTS;
