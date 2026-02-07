const API_BASE_URL = 'http://localhost:5129/api';

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

      return this.handleResponse(response);
    } catch (error) {
      console.error('API Error:', error);
      throw error;
    }
  }

  async handleResponse(response) {
    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.message || 'An error occurred');
    }
    return data;
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

  // Parking endpoints
  async searchParking(params) {
    const queryString = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null)
    ).toString();
    return this.request(`/parking/search?${queryString}`);
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

  async deleteParkingFile(parkingSpaceId, fileName) {
    return this.request(`/files/parking/${parkingSpaceId}/${fileName}`, { method: 'DELETE' });
  }

  async getParkingFiles(parkingSpaceId) {
    return this.request(`/files/parking/${parkingSpaceId}`);
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

  async checkIn(id) {
    return this.request(`/bookings/${id}/check-in`, { method: 'POST' });
  }

  async checkOut(id) {
    return this.request(`/bookings/${id}/check-out`, { method: 'POST' });
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
}

export const api = new ApiService();
export default api;
