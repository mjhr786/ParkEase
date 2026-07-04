import api from './api';

class CorporateService {
  // Helper to get the active company ID
  getCompanyId() {
    return localStorage.getItem('activeCompanyId');
  }

  // Helper to add company header to options is no longer needed since companyId is strictly in route
  // but kept for any legacy usage or cross-company requests
  getHeaders(options = {}) {
    const companyId = this.getCompanyId();
    return {
      ...options,
      headers: {
        ...options.headers,
        ...(companyId && { 'X-Company-Id': companyId })
      }
    };
  }

  // ══════════════════════════════════════════════════════
  // COMPANIES
  // ══════════════════════════════════════════════════════
  
  async getMyCompanies() {
    return api.request('/v1/corporate/me/companies');
  }

  async createCompany(data) {
    return api.request('/v1/corporate/companies', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  async getCompany() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}`);
  }

  async getDashboard() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/dashboard`);
  }

  // ══════════════════════════════════════════════════════
  // MEMBERSHIPS & INVITATIONS
  // ══════════════════════════════════════════════════════

  async getMembers(page = 1, pageSize = 50) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/members?page=${page}&pageSize=${pageSize}`);
  }

  async addMember(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/members`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  async inviteMember(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invitations`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  async acceptInvitation(token) {
    return api.request('/v1/corporate/invitations/accept', {
      method: 'POST',
      body: JSON.stringify(token),
      headers: { 'Content-Type': 'application/json' }
    });
  }

  async removeMember(membershipId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/members/${membershipId}`, {
      method: 'DELETE'
    });
  }

  // ══════════════════════════════════════════════════════
  // ALLOCATIONS & POLICIES
  // ══════════════════════════════════════════════════════

  async getAllocations() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/allocations`);
  }

  async getParkingSpaces() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/parking-spaces`);
  }

  async createParkingSpace(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/parking-spaces`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  async toggleParkingSpace(parkingSpaceId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/parking-spaces/${parkingSpaceId}/toggle-active`, {
      method: 'POST'
    });
  }

  async updateParkingSpace(parkingSpaceId, data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/parking-spaces/${parkingSpaceId}`, {
      method: 'PUT',
      body: JSON.stringify(data)
    });
  }

  async retireParkingSpace(parkingSpaceId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/parking-spaces/${parkingSpaceId}`, {
      method: 'DELETE'
    });
  }

  async createOwnedAllocation(parkingSpaceId, data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/parking-spaces/${parkingSpaceId}/allocations`, {
      method: 'POST',
      body: JSON.stringify({ ...data, parkingSpaceId })
    });
  }

  async requestAllocation(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/allocations`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  // Get pending corporate allocation requests for the active vendor
  async getVendorAllocations() {
    return api.request('/v1/corporate/vendor/allocations');
  }

  // Called by Vendor / Parking Space owner
  async approveAllocation(allocationId) {
    return api.request(`/v1/corporate/allocations/${allocationId}/approve`, {
      method: 'POST'
    });
  }

  // Called by Vendor / Parking Space owner
  async rejectAllocation(allocationId, reason) {
    return api.request(`/v1/corporate/allocations/${allocationId}/reject`, {
      method: 'POST',
      body: JSON.stringify(reason),
      headers: { 'Content-Type': 'application/json' }
    });
  }

  async updatePolicy(allocationId, data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/allocations/${allocationId}/policy`, {
      method: 'PUT',
      body: JSON.stringify(data)
    });
  }

  async assignFixedSlot(allocationId, data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/allocations/${allocationId}/fixed-slots`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  async removeFixedSlot(allocationId, membershipId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/allocations/${allocationId}/fixed-slots/${membershipId}`, {
      method: 'DELETE'
    });
  }

  // ══════════════════════════════════════════════════════
  // BOOKINGS
  // ══════════════════════════════════════════════════════

  async getBookings(page = 1, pageSize = 20) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/bookings?page=${page}&pageSize=${pageSize}`);
  }

  async getWaitlist() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/waitlist`);
  }

  async cancelWaitlistEntry(waitlistEntryId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/waitlist/${waitlistEntryId}`, {
      method: 'DELETE'
    });
  }

  async promoteWaitlistEntry(waitlistEntryId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/waitlist/${waitlistEntryId}/promote`, {
      method: 'POST'
    });
  }

  async bookEmployeeParking(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/bookings/employee`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }

  async bookVisitorParking(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/bookings/visitor`, {
      method: 'POST',
      body: JSON.stringify(data)
    });
  }
}

export default new CorporateService();
