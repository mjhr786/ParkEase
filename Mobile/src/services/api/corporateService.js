import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';

class CorporateService {
    // 16.1 Companies
    createCompany = async (companyData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.COMPANIES, companyData);
        return response.data;
    };

    getMyCompanies = async () => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.MY_COMPANIES);
        return response.data;
    };

    getCompanyDetails = async (companyId) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.COMPANY_BY_ID(companyId));
        return response.data;
    };

    updateCompany = async (companyId, companyData) => {
        const response = await apiClient.put(ENDPOINTS.CORPORATE.COMPANY_BY_ID(companyId), companyData);
        return response.data;
    };

    getDashboard = async (companyId) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.DASHBOARD(companyId));
        return response.data;
    };

    // 16.2 Members & Invitations
    getMembers = async (companyId, params) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.MEMBERS(companyId), { params });
        return response.data;
    };

    addMember = async (companyId, memberData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.MEMBERS(companyId), memberData);
        return response.data;
    };

    updateMember = async (companyId, membershipId, updateData) => {
        const response = await apiClient.put(ENDPOINTS.CORPORATE.MEMBER_BY_ID(companyId, membershipId), updateData);
        return response.data;
    };

    removeMember = async (companyId, membershipId) => {
        const response = await apiClient.delete(ENDPOINTS.CORPORATE.MEMBER_BY_ID(companyId, membershipId));
        return response.data;
    };

    getInvitations = async (companyId) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.INVITATIONS(companyId));
        return response.data;
    };

    inviteMember = async (companyId, inviteData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.INVITATIONS(companyId), inviteData);
        return response.data;
    };

    cancelInvitation = async (companyId, invitationId) => {
        const response = await apiClient.delete(ENDPOINTS.CORPORATE.INVITATION_BY_ID(companyId, invitationId));
        return response.data;
    };

    resendInvitation = async (companyId, invitationId) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.INVITATION_RESEND(companyId, invitationId));
        return response.data;
    };

    acceptInvitation = async (token) => {
        // Body is a raw string token
        const response = await apiClient.post(ENDPOINTS.CORPORATE.ACCEPT_INVITATION, `"${token}"`, {
            headers: { 'Content-Type': 'application/json' }
        });
        return response.data;
    };

    // 16.3 Allocations & Company Parking
    getAllocations = async (companyId) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.ALLOCATIONS(companyId));
        return response.data;
    };

    requestAllocation = async (companyId, requestData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.ALLOCATIONS(companyId), requestData);
        return response.data;
    };

    getVendorAllocations = async () => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.VENDOR_ALLOCATIONS);
        return response.data;
    };

    approveAllocation = async (allocationId) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.ALLOCATION_APPROVE(allocationId));
        return response.data;
    };

    rejectAllocation = async (allocationId, reason) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.ALLOCATION_REJECT(allocationId), `"${reason}"`, {
            headers: { 'Content-Type': 'application/json' }
        });
        return response.data;
    };

    updateAllocationPolicy = async (companyId, allocationId, policyData) => {
        const response = await apiClient.put(ENDPOINTS.CORPORATE.ALLOCATION_POLICY(companyId, allocationId), policyData);
        return response.data;
    };

    assignFixedSlot = async (companyId, allocationId, slotData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.ALLOCATION_FIXED_SLOTS(companyId, allocationId), slotData);
        return response.data;
    };

    removeFixedSlot = async (companyId, allocationId, membershipId) => {
        const response = await apiClient.delete(ENDPOINTS.CORPORATE.ALLOCATION_FIXED_SLOT_DELETE(companyId, allocationId, membershipId));
        return response.data;
    };

    // Company Parking Spaces
    getCompanyParkingSpaces = async (companyId) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.PARKING_SPACES(companyId));
        return response.data;
    };

    createCompanyParkingSpace = async (companyId, spaceData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.PARKING_SPACES(companyId), spaceData);
        return response.data;
    };

    // 16.4 Corporate Bookings
    getCorporateBookings = async (companyId, params) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.BOOKINGS(companyId), { params });
        return response.data;
    };

    createEmployeeBooking = async (companyId, bookingData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.BOOKING_EMPLOYEE(companyId), bookingData);
        return response.data;
    };

    createVisitorBooking = async (companyId, visitorData) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.BOOKING_VISITOR(companyId), visitorData);
        return response.data;
    };

    cancelCorporateBooking = async (companyId, bookingId, reason) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.BOOKING_CANCEL(companyId, bookingId), { reason });
        return response.data;
    };

    // Waitlist
    getWaitlist = async (companyId) => {
        const response = await apiClient.get(ENDPOINTS.CORPORATE.WAITLIST(companyId));
        return response.data;
    };

    cancelWaitlistEntry = async (companyId, waitlistEntryId) => {
        const response = await apiClient.delete(ENDPOINTS.CORPORATE.WAITLIST_BY_ID(companyId, waitlistEntryId));
        return response.data;
    };
    
    promoteWaitlist = async (companyId, waitlistEntryId) => {
        const response = await apiClient.post(ENDPOINTS.CORPORATE.WAITLIST_PROMOTE(companyId, waitlistEntryId));
        return response.data;
    };
}

export const corporateService = new CorporateService();
export default corporateService;
