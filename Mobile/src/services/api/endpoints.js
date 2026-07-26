/**
 * API Endpoints
 * All endpoint constants matching the backend controllers
 */

export const ENDPOINTS = {
    // Auth
    AUTH: {
        REGISTER: '/auth/register',
        LOGIN: '/auth/login',
        GOOGLE_LOGIN: '/auth/google',
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

    // Bookings
    // Keep this in sync with the ASP.NET controller route: /api/bookings.
    // The old /v2/bookings route is no longer served by the backend.
    BOOKINGS: {
        BASE: '/bookings',
        MY_BOOKINGS: '/bookings/my-bookings',
        VENDOR_BOOKINGS: '/bookings/vendor-bookings',
        PENDING_COUNT: '/bookings/pending-count',
        CALCULATE_PRICE: '/bookings/calculate-price',
        BY_ID: (id) => `/bookings/${id}`,
        BY_REFERENCE: (ref) => `/bookings/reference/${ref}`,
        BY_PARKING_SPACE: (parkingSpaceId) => `/bookings/parking-space/${parkingSpaceId}`,
        CANCEL: (id) => `/bookings/${id}/cancel`,
        APPROVE: (id) => `/bookings/${id}/approve`,
        REJECT: (id) => `/bookings/${id}/reject`,
        CHECK_IN: (id) => `/bookings/${id}/check-in`,
        CHECK_OUT: (id) => `/bookings/${id}/check-out`,
        EXTEND: (id) => `/bookings/${id}/extend`,
        APPROVE_EXTENSION: (id) => `/bookings/${id}/approve-extension`,
        REJECT_EXTENSION: (id) => `/bookings/${id}/reject-extension`,
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

    // Chat
    CHAT: {
        CONVERSATIONS: '/chat/conversations',
        MESSAGES: (id) => `/chat/conversations/${id}/messages`,
        SEND: '/chat/send',
        MARK_READ: (id) => `/chat/conversations/${id}/read`,
        UNREAD_COUNT: '/chat/unread-count',
    },

    // Notifications
    NOTIFICATIONS: {
        BASE: '/notifications',
        MARK_READ: (id) => `/notifications/${id}/read`,
        MARK_ALL_READ: '/notifications/read-all',
        DELETE: (id) => `/notifications/${id}`,
        CLEAR_ALL: '/notifications/clear-all',
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

    // Device Tokens (FCM)
    DEVICE_TOKENS: {
        REGISTER: '/device-tokens/register',
        DEREGISTER: '/device-tokens/deregister',
    },

    // Dashboard
    DASHBOARD: {
        VENDOR: '/dashboard/vendor',
        MEMBER: '/dashboard/member',
    },

    // Parking availability forecasts
    PARKING_AVAILABILITY: {
        FORECAST: (parkingSpaceId) => `/parking-availability/${parkingSpaceId}/forecast`,
        MY_LISTINGS: '/parking-availability/my-listings',
    },

    // Parking passes
    PASSES: {
        BASE: '/passes',
        MY: '/passes/my',
        CORPORATE: '/passes/corporate',
    },

    // Files
    FILES: {
        SIGN_UPLOAD: (parkingSpaceId) => `/files/parking/${parkingSpaceId}/sign-upload`,
        CONFIRM_UPLOAD: (parkingSpaceId) => `/files/parking/${parkingSpaceId}/confirm-upload`,
        DELETE: (parkingSpaceId, fileName) => `/files/parking/${parkingSpaceId}/${fileName}`,
        GET: (parkingSpaceId) => `/files/parking/${parkingSpaceId}`,
    },

    // Corporate Module
    CORPORATE: {
        // 16.1 Companies
        COMPANIES: '/v1/corporate/companies',
        COMPANY_BY_ID: (companyId) => `/v1/corporate/companies/${companyId}`,
        MY_COMPANIES: '/v1/corporate/me/companies',
        DASHBOARD: (companyId) => `/v1/corporate/companies/${companyId}/dashboard`,
        DASHBOARD_EXPORT: (companyId) => `/v1/corporate/companies/${companyId}/dashboard/export`,
        
        // 16.2 Members & invitations
        MEMBERS: (companyId) => `/v1/corporate/companies/${companyId}/members`,
        MEMBER_BY_ID: (companyId, membershipId) => `/v1/corporate/companies/${companyId}/members/${membershipId}`,
        INVITATIONS: (companyId) => `/v1/corporate/companies/${companyId}/invitations`,
        INVITATION_BY_ID: (companyId, invitationId) => `/v1/corporate/companies/${companyId}/invitations/${invitationId}`,
        INVITATION_RESEND: (companyId, invitationId) => `/v1/corporate/companies/${companyId}/invitations/${invitationId}/resend`,
        ACCEPT_INVITATION: '/v1/corporate/invitations/accept',

        // 16.3 Allocations & company parking
        ALLOCATIONS: (companyId) => `/v1/corporate/companies/${companyId}/allocations`,
        VENDOR_ALLOCATIONS: '/v1/corporate/vendor/allocations',
        ALLOCATION_APPROVE: (allocationId) => `/v1/corporate/allocations/${allocationId}/approve`,
        ALLOCATION_REJECT: (allocationId) => `/v1/corporate/allocations/${allocationId}/reject`,
        ALLOCATION_POLICY: (companyId, allocationId) => `/v1/corporate/companies/${companyId}/allocations/${allocationId}/policy`,
        ALLOCATION_CONTRACT: (companyId, allocationId) => `/v1/corporate/companies/${companyId}/allocations/${allocationId}/contract`,
        ALLOCATION_FIXED_SLOTS: (companyId, allocationId) => `/v1/corporate/companies/${companyId}/allocations/${allocationId}/fixed-slots`,
        ALLOCATION_FIXED_SLOT_DELETE: (companyId, allocationId, membershipId) => `/v1/corporate/companies/${companyId}/allocations/${allocationId}/fixed-slots/${membershipId}`,
        
        PARKING_SPACES: (companyId) => `/v1/corporate/companies/${companyId}/parking-spaces`,
        PARKING_SPACE_BY_ID: (companyId, parkingSpaceId) => `/v1/corporate/companies/${companyId}/parking-spaces/${parkingSpaceId}`,
        PARKING_SPACE_TOGGLE_ACTIVE: (companyId, parkingSpaceId) => `/v1/corporate/companies/${companyId}/parking-spaces/${parkingSpaceId}/toggle-active`,
        PARKING_SPACE_ALLOCATIONS: (companyId, parkingSpaceId) => `/v1/corporate/companies/${companyId}/parking-spaces/${parkingSpaceId}/allocations`,

        // 16.4 Corporate bookings & waitlist
        BOOKINGS: (companyId) => `/v1/corporate/companies/${companyId}/bookings`,
        BOOKINGS_EXPORT: (companyId) => `/v1/corporate/companies/${companyId}/bookings/export`,
        BOOKING_EMPLOYEE: (companyId) => `/v1/corporate/companies/${companyId}/bookings/employee`,
        BOOKING_VISITOR: (companyId) => `/v1/corporate/companies/${companyId}/bookings/visitor`,
        BOOKING_CANCEL: (companyId, bookingId) => `/v1/corporate/companies/${companyId}/bookings/${bookingId}/cancel`,
        WAITLIST: (companyId) => `/v1/corporate/companies/${companyId}/waitlist`,
        WAITLIST_BY_ID: (companyId, waitlistEntryId) => `/v1/corporate/companies/${companyId}/waitlist/${waitlistEntryId}`,
        WAITLIST_PROMOTE: (companyId, waitlistEntryId) => `/v1/corporate/companies/${companyId}/waitlist/${waitlistEntryId}/promote`,
    },
};

export default ENDPOINTS;
