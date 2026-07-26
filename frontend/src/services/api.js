import { API_ENDPOINTS } from '../config';
import { dispatchAuthChanged } from '../utils/authEvents';

// Use empty string for production (same origin) or localhost for development
const API_BASE_URL = API_ENDPOINTS.BASE;

class ApiService {
  constructor() {
    this.baseUrl = API_BASE_URL;
    /** @type {Promise<boolean>|null} Single-flight refresh so concurrent 401s share one POST /auth/refresh */
    this._refreshPromise = null;
  }

  getToken() {
    return localStorage.getItem('accessToken');
  }

  setTokens(accessToken, refreshToken) {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    // Notify SignalR hooks (same-tab) so they can connect after login/refresh without polling.
    dispatchAuthChanged({ reason: 'tokens-set' });
  }

  clearTokens() {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    // Notify SignalR hooks to disconnect after logout / failed refresh.
    dispatchAuthChanged({ reason: 'tokens-cleared' });
  }

  async request(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const token = this.getToken();

    const headers = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    try {
      const response = await fetch(url, {
        ...options,
        headers,
      });

      if (response.status === 401) {
        // Don't try to refresh token for auth endpoints (login/register)
        // A 401 here means invalid credentials, not an expired token
        const isAuthEndpoint = endpoint.startsWith('/auth/');

        if (!isAuthEndpoint) {
          // Try to refresh token
          const refreshed = await this.refreshToken();
          if (refreshed) {
            headers['Authorization'] = `Bearer ${this.getToken()}`;
            const retryResponse = await fetch(url, { ...options, headers });
            return this.handleResponse(retryResponse);
          }
          this.clearTokens();
          window.location.href = '/login';
          return null;
        }
      }

      return this.handleResponse(response);
    } catch (error) {
      console.error('API Error:', error);
      throw error;
    }
  }

  async handleResponse(response) {
    const contentType = response.headers.get('content-type');

    // Handle JSON responses
    if (contentType && (contentType.includes('application/json') || contentType.includes('application/problem+json'))) {
      const data = await response.json();

      if (!response.ok) {
        // Preserve the entire error response including errors array
        throw {
          response: {
            data: data,
            status: response.status
          },
          message: data.message || `HTTP error! status: ${response.status}`
        };
      }

      return data;
    }

    // Handle non-JSON responses
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return response;
  }

  /**
   * Authenticated fetch that returns a Blob (file downloads / CSV export).
   * @returns {{ blob: Blob, fileName: string|null }}
   */
  async requestBlob(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const token = this.getToken();
    const headers = { ...options.headers };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    let response = await fetch(url, { ...options, headers });

    if (response.status === 401) {
      const refreshed = await this.refreshToken();
      if (refreshed) {
        headers['Authorization'] = `Bearer ${this.getToken()}`;
        response = await fetch(url, { ...options, headers });
      } else {
        this.clearTokens();
        window.location.href = '/login';
        throw new Error('Unauthorized');
      }
    }

    if (!response.ok) {
      const contentType = response.headers.get('content-type') || '';
      if (contentType.includes('application/json')) {
        const data = await response.json();
        throw new Error(data.message || `HTTP error! status: ${response.status}`);
      }
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const disposition = response.headers.get('content-disposition') || '';
    const match = /filename\*?=(?:UTF-8''|")?([^\";]+)/i.exec(disposition);
    const fileName = match ? decodeURIComponent(match[1].replace(/"/g, '')) : null;
    const blob = await response.blob();
    return { blob, fileName };
  }

  async refreshToken() {
    // Coalesce concurrent refresh attempts (many API calls can 401 at once).
    if (this._refreshPromise) {
      return this._refreshPromise;
    }

    this._refreshPromise = this._doRefreshToken().finally(() => {
      this._refreshPromise = null;
    });
    return this._refreshPromise;
  }

  async _doRefreshToken() {
    const refreshToken = localStorage.getItem('refreshToken');
    if (!refreshToken) return false;

    try {
      const body = JSON.stringify({ refreshToken });
      const response = await fetch(`${this.baseUrl}/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
        },
        body,
      });

      if (response.ok) {
        const data = await response.json();
        if (data.success && data.data?.accessToken && data.data?.refreshToken) {
          this.setTokens(data.data.accessToken, data.data.refreshToken);
          return true;
        }
      }
      return false;
    } catch {
      return false;
    }
  }

  // Auth endpoints
  async register(data) {
    return this.request('/auth/register', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async login(data) {
    return this.request('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async logout() {
    return this.request('/auth/logout', { method: 'POST' });
  }

  // User endpoints
  async getCurrentUser() {
    return this.request('/users/me');
  }

  async updateProfile(data) {
    return this.request('/users/me', {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async changePassword(data) {
    return this.request('/auth/change-password', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async deleteProfile() {
    return this.request('/users/me', { method: 'DELETE' });
  }

  // Payment endpoints
  async getStripeConfig() {
    return this.request('/payments/stripe-config');
  }

  async createPaymentOrder(bookingId, { payOverstayFee } = {}) {
    const body = payOverstayFee
      ? { bookingId, payOverstayFee: true }
      : bookingId;
    return this.request('/payments/create-order', {
      method: 'POST',
      body: JSON.stringify(body),
    });
  }

  async verifyPayment(data) {
    return this.request('/payments/verify', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Favorite endpoints
  async getMyFavorites() {
    return this.request('/favorites');
  }

  async toggleFavorite(parkingSpaceId) {
    return this.request(`/favorites/${parkingSpaceId}/toggle`, {
      method: 'POST'
    });
  }

  // Parking endpoints
  async searchParking(params) {
    const queryString = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null)
    ).toString();
    return this.request(`/parking/search?${queryString}`);
  }

  async getMapParking(params) {
    const queryString = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null)
    ).toString();
    return this.request(`/parking/map?${queryString}`);
  }

  async getParkingById(id) {
    return this.request(`/parking/${id}`);
  }

  async getMyListings() {
    return this.request('/parking/my-listings');
  }

  async getParkingAvailabilityForecast(parkingSpaceId, params = {}) {
    const queryString = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null)
    ).toString();
    const suffix = queryString ? `?${queryString}` : '';
    return this.request(`/parking-availability/${parkingSpaceId}/forecast${suffix}`);
  }

  async getMyListingAvailabilityForecasts(params = {}) {
    const queryString = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null)
    ).toString();
    const suffix = queryString ? `?${queryString}` : '';
    return this.request(`/parking-availability/my-listings${suffix}`);
  }

  async createParking(data) {
    return this.request('/parking', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateParking(id, data) {
    return this.request(`/parking/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deleteParking(id) {
    return this.request(`/parking/${id}`, { method: 'DELETE' });
  }

  // File upload endpoints
  async uploadParkingFiles(parkingSpaceId, files) {
    const formData = new FormData();
    files.forEach(file => formData.append('files', file));

    const token = this.getToken();
    const response = await fetch(`${this.baseUrl}/files/parking/${parkingSpaceId}/upload`, {
      method: 'POST',
      headers: {
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      },
      body: formData,
    });

    return this.handleResponse(response);
  }

  async getPresignedUrl(parkingSpaceId, fileName, contentType) {
    return this.request(`/files/parking/${parkingSpaceId}/sign-upload`, {
      method: 'POST',
      body: JSON.stringify({ fileName, contentType })
    });
  }

  async confirmUpload(parkingSpaceId, fileUrls) {
    return this.request(`/files/parking/${parkingSpaceId}/confirm-upload`, {
      method: 'POST',
      body: JSON.stringify({ fileUrls })
    });
  }

  async deleteParkingFile(parkingSpaceId, fileName) {
    return this.request(`/files/parking/${parkingSpaceId}/${fileName}`, { method: 'DELETE' });
  }

  async getParkingFiles(parkingSpaceId) {
    return this.request(`/files/parking/${parkingSpaceId}`);
  }

  // Review endpoints
  async getReviewsByParkingSpace(parkingSpaceId) {
    return this.request(`/reviews/parking-space/${parkingSpaceId}`);
  }

  async createReview(data) {
    return this.request('/reviews', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateReview(id, data) {
    return this.request(`/reviews/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deleteReview(id) {
    return this.request(`/reviews/${id}`, { method: 'DELETE' });
  }

  async addOwnerResponse(id, response) {
    return this.request(`/reviews/${id}/owner-response`, {
      method: 'POST',
      body: JSON.stringify({ response }),
    });
  }

  // Event parking packages
  async getEventPackagesOnSale(take = 50) {
    return this.request(`/event-packages/on-sale?take=${take}`);
  }

  async getEventVenuesOnSale(take = 50) {
    return this.request(`/event-packages/venues/on-sale?take=${take}`);
  }

  async getEventPackagesByVenueEvent(venueEventId, activeOnly = true) {
    return this.request(`/event-packages/by-venue-event/${venueEventId}?activeOnly=${activeOnly}`);
  }

  async getEventPackagesByParking(parkingSpaceId, activeOnly = true) {
    return this.request(`/event-packages/by-parking/${parkingSpaceId}?activeOnly=${activeOnly}`);
  }

  async getMyEventPackages() {
    return this.request('/event-packages/my');
  }

  async getMyEventPackageAnalytics() {
    return this.request('/event-packages/my/analytics');
  }

  async getEventPackageAnalytics(id) {
    return this.request(`/event-packages/${id}/analytics`);
  }

  async createEventPackage(data) {
    return this.request('/event-packages', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateEventPackage(id, data) {
    return this.request(`/event-packages/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deactivateEventPackage(id) {
    return this.request(`/event-packages/${id}/deactivate`, { method: 'POST' });
  }

  async purchaseEventPackage(id, data) {
    return this.request(`/event-packages/${id}/purchase`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Ancillary services (car wash / detailing add-ons)
  async getAncillaryServicesByParking(parkingSpaceId, activeOnly = true) {
    return this.request(`/ancillary-services/by-parking/${parkingSpaceId}?activeOnly=${activeOnly}`);
  }

  async getMyAncillaryServices() {
    return this.request('/ancillary-services/my');
  }

  async createAncillaryService(data) {
    return this.request('/ancillary-services', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateAncillaryService(id, data) {
    return this.request(`/ancillary-services/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deactivateAncillaryService(id) {
    return this.request(`/ancillary-services/${id}/deactivate`, { method: 'POST' });
  }

  // Booking endpoints
  async calculatePrice(data) {
    return this.request('/bookings/calculate-price', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async createBooking(data) {
    return this.request('/bookings', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async getMyBookings(params = {}) {
    const queryString = new URLSearchParams(params).toString();
    return this.request(`/bookings/my-bookings?${queryString}`);
  }

  async getBookingById(id) {
    return this.request(`/bookings/${id}`);
  }

  async cancelBooking(id, reason) {
    return this.request(`/bookings/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  // Request an extension (creates a pending extension request for vendor approval)
  async requestExtension(id, data) {
    return this.request(`/bookings/${id}/extend`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Keep alias for backwards compatibility
  async extendBooking(id, data) {
    return this.requestExtension(id, data);
  }

  // Vendor: approve a pending extension request
  async approveExtension(id) {
    return this.request(`/bookings/${id}/approve-extension`, { method: 'POST' });
  }

  // Vendor: reject a pending extension request
  async rejectExtension(id, reason) {
    return this.request(`/bookings/${id}/reject-extension`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  async getAccessPass(bookingId) {
    return this.request(`/bookings/${bookingId}/access-pass`);
  }

  /**
   * Download Apple Wallet .pkpass as a Blob (authenticated).
   * @returns {Promise<{ blob: Blob, fileName: string }>}
   */
  async downloadAppleWalletPass(bookingId) {
    const url = `${this.baseUrl}/bookings/${bookingId}/access-pass/apple.pkpass`;
    const token = this.getToken();
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;

    let response = await fetch(url, { headers });
    if (response.status === 401) {
      const refreshed = await this.refreshToken();
      if (refreshed) {
        headers['Authorization'] = `Bearer ${this.getToken()}`;
        response = await fetch(url, { headers });
      } else {
        this.clearTokens();
        window.location.href = '/login';
        throw new Error('Unauthorized');
      }
    }

    if (!response.ok) {
      let message = 'Apple Wallet download failed';
      try {
        const err = await response.json();
        message = err.message || message;
      } catch {
        /* ignore */
      }
      throw new Error(message);
    }

    const disposition = response.headers.get('content-disposition') || '';
    const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/i.exec(disposition);
    const fileName = match
      ? match[1].replace(/['"]/g, '')
      : `ParkEase-${bookingId}.pkpass`;
    const blob = await response.blob();
    return { blob, fileName };
  }

  async getGoogleWalletSaveLink(bookingId) {
    return this.request(`/bookings/${bookingId}/access-pass/google-wallet`);
  }

  async verifyAccessPass(token) {
    return this.request('/bookings/access-pass/verify', {
      method: 'POST',
      body: JSON.stringify({ token }),
    });
  }

  async checkIn(id) {
    return this.request(`/bookings/${id}/check-in`, { method: 'POST' });
  }

  async checkOut(id) {
    return this.request(`/bookings/${id}/check-out`, { method: 'POST' });
  }

  async requestValet(id, data = {}) {
    return this.request(`/bookings/${id}/valet/request`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async cancelValet(id) {
    return this.request(`/bookings/${id}/valet/cancel`, { method: 'POST' });
  }

  async acknowledgeValet(id) {
    return this.request(`/bookings/${id}/valet/acknowledge`, { method: 'POST' });
  }

  async markValetReady(id) {
    return this.request(`/bookings/${id}/valet/ready`, { method: 'POST' });
  }

  async completeValet(id) {
    return this.request(`/bookings/${id}/valet/complete`, { method: 'POST' });
  }

  async assignBay(id, data) {
    return this.request(`/bookings/${id}/bay-assignment`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async getVendorBookings(params = {}) {
    const queryString = new URLSearchParams(params).toString();
    return this.request(`/bookings/vendor-bookings?${queryString}`);
  }

  async approveBooking(id) {
    return this.request(`/bookings/${id}/approve`, { method: 'POST' });
  }

  async rejectBooking(id, reason) {
    return this.request(`/bookings/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  async getPendingRequestsCount() {
    return this.request('/bookings/pending-count');
  }

  // Payment endpoints
  async processPayment(data) {
    return this.request('/payments', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Review endpoints
  async getReviews(parkingSpaceId) {
    return this.request(`/reviews/parking-space/${parkingSpaceId}`);
  }

  async createReview(data) {
    return this.request('/reviews', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Dashboard endpoints
  async getVendorDashboard() {
    return this.request('/dashboard/vendor');
  }

  async getMemberDashboard() {
    return this.request('/dashboard/member');
  }

  // Chat endpoints
  async getConversations(page = 1, pageSize = 20) {
    return this.request(`/chat/conversations?page=${page}&pageSize=${pageSize}`);
  }

  async getMessages(conversationId, page = 1, pageSize = 50) {
    return this.request(`/chat/conversations/${conversationId}/messages?page=${page}&pageSize=${pageSize}`);
  }

  async sendMessage(data) {
    return this.request('/chat/send', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async markAsRead(conversationId) {
    return this.request(`/chat/conversations/${conversationId}/read`, {
      method: 'POST',
    });
  }

  async getUnreadCount() {
    return this.request('/chat/unread-count');
  }

  // Notification Center endpoints
  async getNotifications(page = 1, pageSize = 20) {
    return this.request(`/notifications?page=${page}&pageSize=${pageSize}`);
  }

  async markNotificationAsRead(notificationId) {
    return this.request(`/notifications/${notificationId}/read`, {
      method: 'PUT',
    });
  }

  async markAllNotificationsAsRead() {
    return this.request('/notifications/read-all', {
      method: 'PUT',
    });
  }

  async deleteNotification(notificationId) {
    return this.request(`/notifications/${notificationId}`, {
      method: 'DELETE',
    });
  }

  async clearAllNotifications() {
    return this.request('/notifications/clear-all', {
      method: 'DELETE',
    });
  }

  // Vehicle endpoints
  async getMyVehicles() {
    return this.request('/vehicles');
  }

  async addVehicle(data) {
    return this.request('/vehicles', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateVehicle(id, data) {
    return this.request(`/vehicles/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deleteVehicle(id) {
    return this.request(`/vehicles/${id}`, { method: 'DELETE' });
  }

  // Admin — transactional outbox
  async getOutboxMessages({ status, type, page = 1, pageSize = 50 } = {}) {
    const params = new URLSearchParams();
    if (status !== undefined && status !== null && status !== '') params.set('status', status);
    if (type) params.set('type', type);
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    return this.request(`/admin/outbox?${params.toString()}`);
  }

  async getOutboxMessage(id) {
    return this.request(`/admin/outbox/${id}`);
  }

  async requeueOutboxMessage(id) {
    return this.request(`/admin/outbox/${id}/requeue`, { method: 'POST' });
  }

  async requeueAllFailedOutbox() {
    return this.request('/admin/outbox/requeue-failed', { method: 'POST' });
  }

  async processOutboxNow(batchSize = 50) {
    return this.request(`/admin/outbox/process?batchSize=${batchSize}`, { method: 'POST' });
  }

  // Admin — LPR simulator (ticketless access)
  async simulateLprEvent({ licensePlate, parkingSpaceId, direction, occurredAtUtc }) {
    return this.request('/iot/lpr-events/simulate', {
      method: 'POST',
      body: JSON.stringify({
        licensePlate,
        parkingSpaceId,
        direction,
        occurredAtUtc: occurredAtUtc || null,
      }),
    });
  }

  /** Mock OCPP: full charge session (start → meter → stop + settle kWh fee). */
  async simulateEvChargingSession({ bookingId, energyKwh, stationId, connectorId }) {
    return this.request('/iot/ocpp/simulate', {
      method: 'POST',
      body: JSON.stringify({
        bookingId,
        energyKwh,
        stationId: stationId || null,
        connectorId: connectorId ?? 1,
      }),
    });
  }

  async getEvChargingSession(bookingId) {
    return this.request(`/bookings/${bookingId}/ev-session`);
  }

  // Vendor — LPR facility registry (camera keys + plate rules)
  async getLprCameraKeys(parkingSpaceId) {
    return this.request(`/parking/${parkingSpaceId}/lpr/camera-keys`);
  }

  async createLprCameraKey(parkingSpaceId, { name, keyId }) {
    return this.request(`/parking/${parkingSpaceId}/lpr/camera-keys`, {
      method: 'POST',
      body: JSON.stringify({ name, keyId: keyId || null }),
    });
  }

  async setLprCameraKeyEnabled(parkingSpaceId, cameraKeyId, isEnabled) {
    return this.request(`/parking/${parkingSpaceId}/lpr/camera-keys/${cameraKeyId}/enabled`, {
      method: 'PUT',
      body: JSON.stringify({ isEnabled }),
    });
  }

  async deleteLprCameraKey(parkingSpaceId, cameraKeyId) {
    return this.request(`/parking/${parkingSpaceId}/lpr/camera-keys/${cameraKeyId}`, {
      method: 'DELETE',
    });
  }

  async getLprPlateRules(parkingSpaceId) {
    return this.request(`/parking/${parkingSpaceId}/lpr/plate-rules`);
  }

  async createLprPlateRule(parkingSpaceId, { licensePlate, ruleType, note }) {
    return this.request(`/parking/${parkingSpaceId}/lpr/plate-rules`, {
      method: 'POST',
      body: JSON.stringify({ licensePlate, ruleType, note: note || null }),
    });
  }

  async setLprPlateRuleEnabled(parkingSpaceId, ruleId, isEnabled) {
    return this.request(`/parking/${parkingSpaceId}/lpr/plate-rules/${ruleId}/enabled`, {
      method: 'PUT',
      body: JSON.stringify({ isEnabled }),
    });
  }

  async deleteLprPlateRule(parkingSpaceId, ruleId) {
    return this.request(`/parking/${parkingSpaceId}/lpr/plate-rules/${ruleId}`, {
      method: 'DELETE',
    });
  }
}

export const api = new ApiService();
export default api;
