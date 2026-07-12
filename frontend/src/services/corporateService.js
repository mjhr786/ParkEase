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

  async updateCompany(data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}`, {
      method: 'PUT',
      body: JSON.stringify(data)
    });
  }

  async getDashboard() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/dashboard`);
  }

  async getInvitations() {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invitations`);
  }

  async cancelInvitation(invitationId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invitations/${invitationId}`, {
      method: 'DELETE'
    });
  }

  async resendInvitation(invitationId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invitations/${invitationId}/resend`, {
      method: 'POST'
    });
  }

  async exportDashboard() {
    return api.requestBlob(`/v1/corporate/companies/${this.getCompanyId()}/dashboard/export`);
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

  async updateMember(membershipId, data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/members/${membershipId}`, {
      method: 'PUT',
      body: JSON.stringify(data)
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

  async updateAllocationContract(allocationId, data) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/allocations/${allocationId}/contract`, {
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

  async getBookings(page = 1, pageSize = 20, { status, isVisitor, fromUtc, toUtc } = {}) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status !== undefined && status !== null && status !== '' && status !== 'all') {
      params.set('status', String(status));
    }
    if (isVisitor === true || isVisitor === false) {
      params.set('isVisitor', String(isVisitor));
    }
    if (fromUtc) params.set('fromUtc', fromUtc);
    if (toUtc) params.set('toUtc', toUtc);
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/bookings?${params.toString()}`);
  }

  /**
   * Download CSV export for corporate bookings (returns blob response).
   */
  async exportBookings({ status, isVisitor, fromUtc, toUtc } = {}) {
    const params = new URLSearchParams();
    if (status !== undefined && status !== null && status !== '' && status !== 'all') {
      params.set('status', String(status));
    }
    if (isVisitor === true || isVisitor === false) {
      params.set('isVisitor', String(isVisitor));
    }
    if (fromUtc) params.set('fromUtc', fromUtc);
    if (toUtc) params.set('toUtc', toUtc);
    const qs = params.toString();
    const path = `/v1/corporate/companies/${this.getCompanyId()}/bookings/export${qs ? `?${qs}` : ''}`;
    return api.requestBlob(path);
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

  async cancelBooking(bookingId, reason = 'Cancelled from corporate bookings') {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/bookings/${bookingId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason })
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

  // ══════════════════════════════════════════════════════
  // INVOICES
  // ══════════════════════════════════════════════════════

  async getInvoices(page = 1, pageSize = 20, status = null) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status !== undefined && status !== null && status !== '' && status !== 'all') {
      params.set('status', String(status));
    }
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invoices?${params.toString()}`);
  }

  async getInvoice(invoiceId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invoices/${invoiceId}`);
  }

  async generateInvoice({ periodStart, periodEnd }) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invoices`, {
      method: 'POST',
      body: JSON.stringify({ periodStart, periodEnd })
    });
  }

  async issueInvoice(invoiceId) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invoices/${invoiceId}/issue`, {
      method: 'POST'
    });
  }

  async markInvoicePaid(invoiceId, { paymentReference, paymentNotes } = {}) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invoices/${invoiceId}/mark-paid`, {
      method: 'POST',
      body: JSON.stringify({ paymentReference, paymentNotes })
    });
  }

  async voidInvoice(invoiceId, { reason }) {
    return api.request(`/v1/corporate/companies/${this.getCompanyId()}/invoices/${invoiceId}/void`, {
      method: 'POST',
      body: JSON.stringify({ reason })
    });
  }

  async exportInvoice(invoiceId) {
    return api.requestBlob(`/v1/corporate/companies/${this.getCompanyId()}/invoices/${invoiceId}/export`);
  }
}

export default new CorporateService();
