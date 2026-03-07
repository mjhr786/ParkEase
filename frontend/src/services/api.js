import { API_ENDPOINTS } from '../config';

// Use empty string for production (same origin) or localhost for development
const API_BASE_URL = API_ENDPOINTS.BASE;

class ApiService {
  constructor() {
    this.baseUrl = API_BASE_URL;
  }

  getToken() {
    return localStorage.getItem('accessToken');
  }

  setTokens(accessToken, refreshToken) {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
  }

  clearTokens() {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
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

  async refreshToken() {
    const refreshToken = localStorage.getItem('refreshToken');
    if (!refreshToken) return false;

    try {
      const response = await fetch(`${this.baseUrl}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });

      if (response.ok) {
        const data = await response.json();
        if (data.success && data.data) {
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

  async createPaymentOrder(bookingId) {
    return this.request('/payments/create-order', {
      method: 'POST',
      body: JSON.stringify(bookingId),
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

  // Booking endpoints
  async calculatePrice(data) {
    return this.request('/v2/bookings/calculate-price', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async createBooking(data) {
    return this.request('/v2/bookings', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async getMyBookings(params = {}) {
    const queryString = new URLSearchParams(params).toString();
    return this.request(`/v2/bookings/my-bookings?${queryString}`);
  }

  async getBookingById(id) {
    return this.request(`/v2/bookings/${id}`);
  }

  async cancelBooking(id, reason) {
    return this.request(`/v2/bookings/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  // Request an extension (creates a pending extension request for vendor approval)
  async requestExtension(id, data) {
    return this.request(`/v2/bookings/${id}/extend`, {
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
    return this.request(`/v2/bookings/${id}/approve-extension`, { method: 'POST' });
  }

  // Vendor: reject a pending extension request
  async rejectExtension(id, reason) {
    return this.request(`/v2/bookings/${id}/reject-extension`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  async checkIn(id) {
    return this.request(`/v2/bookings/${id}/check-in`, { method: 'POST' });
  }

  async checkOut(id) {
    return this.request(`/v2/bookings/${id}/check-out`, { method: 'POST' });
  }

  async getVendorBookings(params = {}) {
    const queryString = new URLSearchParams(params).toString();
    return this.request(`/v2/bookings/vendor-bookings?${queryString}`);
  }

  async approveBooking(id) {
    return this.request(`/v2/bookings/${id}/approve`, { method: 'POST' });
  }

  async rejectBooking(id, reason) {
    return this.request(`/v2/bookings/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  async getPendingRequestsCount() {
    return this.request('/v2/bookings/pending-count');
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
}

export const api = new ApiService();
export default api;
