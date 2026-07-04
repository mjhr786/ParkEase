
import { useState, useEffect } from 'react';
import corporateService from '../services/corporateService';
import { useCompany } from '../contexts/CompanyContext';

export function useCorporate() {
  const { activeCompanyId } = useCompany();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Dashboard
  const [dashboard, setDashboard] = useState(null);

  const fetchDashboard = async () => {
    if (!activeCompanyId) {
      setError('No company selected');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.getDashboard();
      if (response?.success) {
        setDashboard(response.data);
      } else {
        setError(response?.message || 'Failed to fetch dashboard data');
      }
    } catch (err) {
      setError(err.message || 'Failed to fetch dashboard data');
    } finally {
      setLoading(false);
    }
  };

  // Employees
  const [employees, setEmployees] = useState([]);

  const fetchEmployees = async () => {
    if (!activeCompanyId) return;

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.getMembers();
      if (response?.success) {
        setEmployees(response.data?.members || []);
      } else {
        setError(response?.message || 'Failed to fetch employees');
      }
    } catch (err) {
      setError(err.message || 'Failed to fetch employees');
    } finally {
      setLoading(false);
    }
  };

  const addEmployee = async (employeeData) => {
    if (!activeCompanyId) {
      setError('No company selected');
      return { success: false };
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.addMember(employeeData);
      if (response?.success) {
        // Optimistic update
        setEmployees(prev => [...prev, response.data]);
        return { success: true, data: response.data };
      } else {
        setError(response?.message || 'Failed to add employee');
        return { success: false, message: response?.message };
      }
    } catch (err) {
      setError(err.message || 'Failed to add employee');
      return { success: false, message: err.message };
    } finally {
      setLoading(false);
    }
  };

  const removeEmployee = async (employeeId) => {
    if (!activeCompanyId) {
      setError('No company selected');
      return { success: false };
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.removeMember(employeeId);
      if (response?.success) {
        // Optimistic update
        setEmployees(prev => prev.filter(emp => emp.id !== employeeId));
        return { success: true };
      } else {
        setError(response?.message || 'Failed to remove employee');
        return { success: false, message: response?.message };
      }
    } catch (err) {
      setError(err.message || 'Failed to remove employee');
      return { success: false, message: err.message };
    } finally {
      setLoading(false);
    }
  };

  // Allocations
  const [allocations, setAllocations] = useState([]);

  const fetchAllocations = async (params = {}) => {
    if (!activeCompanyId) return;

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.getAllocations(params);
      if (response?.success) {
        setAllocations(response.data);
      } else {
        setError(response?.message || 'Failed to fetch allocations');
      }
    } catch (err) {
      setError(err.message || 'Failed to fetch allocations');
    } finally {
      setLoading(false);
    }
  };

  const allocateSlots = async (allocationData) => {
    if (!activeCompanyId) {
      setError('No company selected');
      return { success: false };
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.requestAllocation(allocationData);
      if (response?.success) {
        // Optimistic update
        setAllocations(prev => [...prev, response.data]);
        return { success: true, data: response.data };
      } else {
        setError(response?.message || 'Failed to allocate slots');
        return { success: false, message: response?.message };
      }
    } catch (err) {
      setError(err.message || 'Failed to allocate slots');
      return { success: false, message: err.message };
    } finally {
      setLoading(false);
    }
  };

  // Bookings
  const [bookings, setBookings] = useState([]);

  const fetchBookings = async (params = {}) => {
    if (!activeCompanyId) return;

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.getBookings(params?.page, params?.pageSize);
      if (response?.success) {
        setBookings(response.data?.bookings || []);
      } else {
        setError(response?.message || 'Failed to fetch bookings');
      }
    } catch (err) {
      setError(err.message || 'Failed to fetch bookings');
    } finally {
      setLoading(false);
    }
  };

  const createBooking = async (bookingData) => {
    if (!activeCompanyId) {
      setError('No company selected');
      return { success: false };
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.bookEmployeeParking(bookingData);
      if (response?.success) {
        // Optimistic update
        setBookings(prev => [...prev, response.data]);
        return { success: true, data: response.data };
      } else {
        setError(response?.message || 'Failed to create booking');
        return { success: false, message: response?.message };
      }
    } catch (err) {
      setError(err.message || 'Failed to create booking');
      return { success: false, message: err.message };
    } finally {
      setLoading(false);
    }
  };

  const bookVisitor = async (visitorData) => {
    if (!activeCompanyId) {
      setError('No company selected');
      return { success: false };
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.bookVisitorParking(visitorData);
      if (response?.success) {
        return { success: true, data: response.data };
      } else {
        setError(response?.message || 'Failed to book visitor');
        return { success: false, message: response?.message };
      }
    } catch (err) {
      setError(err.message || 'Failed to book visitor');
      return { success: false, message: err.message };
    } finally {
      setLoading(false);
    }
  };

  // Policy
  const [policy, setPolicy] = useState(null);

  const updatePolicy = async (allocationId, policyData) => {
    if (!activeCompanyId) {
      setError('No company selected');
      return { success: false };
    }

    setLoading(true);
    setError(null);
    try {
      const response = await corporateService.updatePolicy(allocationId, policyData);
      if (response?.success) {
        setPolicy(response.data);
        return { success: true, data: response.data };
      } else {
        setError(response?.message || 'Failed to update policy');
        return { success: false, message: response?.message };
      }
    } catch (err) {
      setError(err.message || 'Failed to update policy');
      return { success: false, message: err.message };
    } finally {
      setLoading(false);
    }
  };

  return {
    // State
    loading,
    error,
    dashboard,
    employees,
    allocations,
    bookings,
    policy,

    // Dashboard
    fetchDashboard,

    // Employees
    fetchEmployees,
    addEmployee,
    removeEmployee,

    // Allocations
    fetchAllocations,
    allocateSlots,

    // Bookings
    fetchBookings,
    createBooking,
    bookVisitor,

    // Policy
    updatePolicy
  };
}
