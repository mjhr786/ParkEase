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
        MY_LISTINGS: '/parking/my-listings',
        BY_ID: (id) => `/parking/${id}`,
        TOGGLE_ACTIVE: (id) => `/parking/${id}/toggle-active`,
    },

    // Bookings
    BOOKINGS: {
        BASE: '/bookings',
        MY_BOOKINGS: '/bookings/my-bookings',
        VENDOR_BOOKINGS: '/bookings/vendor-bookings',
        CALCULATE_PRICE: '/bookings/calculate-price',
        BY_ID: (id) => `/bookings/${id}`,
        BY_REFERENCE: (ref) => `/bookings/reference/${ref}`,
        BY_PARKING_SPACE: (id) => `/bookings/parking-space/${id}`,
        CANCEL: (id) => `/bookings/${id}/cancel`,
        CHECK_IN: (id) => `/bookings/${id}/check-in`,
        CHECK_OUT: (id) => `/bookings/${id}/check-out`,
        APPROVE: (id) => `/bookings/${id}/approve`,
        REJECT: (id) => `/bookings/${id}/reject`,
    },

    // Payments
    PAYMENTS: {
        BASE: '/payments',
        STRIPE_CONFIG: '/payments/stripe-config',
        CREATE_ORDER: '/payments/create-order',
        VERIFY: '/payments/verify',
        REFUND: '/payments/refund',
        BY_ID: (id) => `/payments/${id}`,
        BY_BOOKING: (bookingId) => `/payments/booking/${bookingId}`,
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
