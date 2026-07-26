import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import VendorBookings from './VendorBookings';

const mockGetVendorBookings = vi.fn();
const mockApproveBooking = vi.fn();
const mockRejectBooking = vi.fn();
const mockApproveExtension = vi.fn();
const mockGetVendorAllocations = vi.fn();
const mockApproveAllocation = vi.fn();
const mockRejectAllocation = vi.fn();
const mockSubscribeToRefresh = vi.fn(() => () => {});
const mockTriggerRefresh = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

vi.mock('../context/NotificationContext', () => ({
  useNotificationContext: () => ({
    subscribeToRefresh: (...args) => mockSubscribeToRefresh(...args),
    triggerRefresh: (...args) => mockTriggerRefresh(...args),
  }),
}));

vi.mock('../services/api', () => ({
  default: {
    getVendorBookings: (...args) => mockGetVendorBookings(...args),
    approveBooking: (...args) => mockApproveBooking(...args),
    rejectBooking: (...args) => mockRejectBooking(...args),
    approveExtension: (...args) => mockApproveExtension(...args),
    rejectExtension: vi.fn(),
    assignBay: vi.fn(),
    acknowledgeValet: vi.fn(),
    markValetReady: vi.fn(),
    completeValet: vi.fn(),
  },
}));

vi.mock('../services/corporateService', () => ({
  default: {
    getVendorAllocations: (...args) => mockGetVendorAllocations(...args),
    approveAllocation: (...args) => mockApproveAllocation(...args),
    rejectAllocation: (...args) => mockRejectAllocation(...args),
  },
}));

vi.mock('../utils/toast.jsx', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

function renderVendorBookings() {
  return render(
    <MemoryRouter>
      <VendorBookings />
    </MemoryRouter>
  );
}

const pendingBooking = {
  id: 'vb-1',
  parkingSpaceTitle: 'Vendor Lot A',
  userName: 'Guest User',
  status: 0,
  totalAmount: 150,
  bookingReference: 'VB-100',
  startDateTime: '2026-07-26T10:00:00Z',
  endDateTime: '2026-07-26T12:00:00Z',
  vehicleNumber: 'MH01CD5678',
  pricingType: 0,
  parkingSpaceId: 'ps-v1',
};

const extensionBooking = {
  ...pendingBooking,
  id: 'vb-2',
  status: 8,
  bookingReference: 'VB-EXT',
};

describe('VendorBookings page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSubscribeToRefresh.mockReturnValue(() => {});
    mockGetVendorBookings.mockResolvedValue({ success: true, data: [] });
    mockGetVendorAllocations.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders Vendor Inbox heading and scan-pass link', async () => {
    renderVendorBookings();
    expect(screen.getByRole('heading', { name: /vendor inbox/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /scan pass/i })).toHaveAttribute('href', '/my/access-scan');
    await waitFor(() => expect(mockGetVendorBookings).toHaveBeenCalled());
  });

  it('shows empty state when no pending requests', async () => {
    renderVendorBookings();
    await waitFor(() => {
      expect(screen.getByText(/nothing to review/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/no pending booking or extension requests/i)).toBeInTheDocument();
  });

  it('loads pending and extension-pending requests', async () => {
    mockGetVendorBookings
      .mockResolvedValueOnce({ success: true, data: [pendingBooking] })
      .mockResolvedValueOnce({ success: true, data: [extensionBooking] });

    renderVendorBookings();

    await waitFor(() => {
      expect(screen.getByText('VB-100')).toBeInTheDocument();
    });
    expect(mockGetVendorBookings).toHaveBeenCalledWith({ status: 0, pageSize: 50 });
    expect(mockGetVendorBookings).toHaveBeenCalledWith({ status: 8, pageSize: 50 });
    expect(screen.getByText('VB-EXT')).toBeInTheDocument();
    expect(screen.getAllByText('Vendor Lot A').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/guest user/i).length).toBeGreaterThanOrEqual(1);
  });

  it('approves a pending booking', async () => {
    const user = userEvent.setup();
    mockGetVendorBookings
      .mockResolvedValueOnce({ success: true, data: [pendingBooking] })
      .mockResolvedValueOnce({ success: true, data: [] });
    mockApproveBooking.mockResolvedValue({ success: true });
    // refresh after approve
    mockGetVendorBookings.mockResolvedValue({ success: true, data: [] });

    renderVendorBookings();
    await waitFor(() => expect(screen.getByText('Vendor Lot A')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /approve/i }));

    await waitFor(() => {
      expect(mockApproveBooking).toHaveBeenCalledWith('vb-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Booking approved successfully!');
      expect(mockTriggerRefresh).toHaveBeenCalledWith('booking.approved');
    });
  });

  it('opens reject modal and rejects booking with reason', async () => {
    const user = userEvent.setup();
    mockGetVendorBookings
      .mockResolvedValueOnce({ success: true, data: [pendingBooking] })
      .mockResolvedValueOnce({ success: true, data: [] });
    mockRejectBooking.mockResolvedValue({ success: true });
    mockGetVendorBookings.mockResolvedValue({ success: true, data: [] });

    renderVendorBookings();
    await waitFor(() => expect(screen.getByText('Vendor Lot A')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /reject/i }));

    const reasonInput = await screen.findByPlaceholderText(/reason/i);
    await user.type(reasonInput, 'Lot full');
    await user.click(screen.getByRole('button', { name: /confirm reject/i }));

    await waitFor(() => {
      expect(mockRejectBooking).toHaveBeenCalledWith('vb-1', 'Lot full');
      expect(mockToastSuccess).toHaveBeenCalledWith('Booking rejected');
    });
  });

  it('approves extension request', async () => {
    const user = userEvent.setup();
    mockGetVendorBookings
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValueOnce({ success: true, data: [extensionBooking] });
    mockApproveExtension.mockResolvedValue({ success: true });
    mockGetVendorBookings.mockResolvedValue({ success: true, data: [] });

    renderVendorBookings();
    await waitFor(() => expect(screen.getByText('VB-EXT')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /approve extension/i }));

    await waitFor(() => {
      expect(mockApproveExtension).toHaveBeenCalledWith('vb-2');
      expect(mockToastSuccess).toHaveBeenCalledWith('Extension approved successfully!');
    });
  });

  it('loads corporate allocations when filter changes', async () => {
    const user = userEvent.setup();
    mockGetVendorAllocations.mockResolvedValue({
      success: true,
      data: [
        {
          id: 'alloc-1',
          companyName: 'Acme Corp',
          parkingSpaceTitle: 'Corporate Lot',
          status: 0,
          totalSlots: 5,
          fixedSlots: 2,
          sharedSlots: 3,
          monthlyRate: 10000,
          startDate: '2026-08-01',
          endDate: '2026-08-31',
        },
      ],
    });

    renderVendorBookings();
    await waitFor(() => expect(mockGetVendorBookings).toHaveBeenCalled());

    await user.selectOptions(screen.getByRole('combobox'), 'allocations');

    await waitFor(() => {
      expect(mockGetVendorAllocations).toHaveBeenCalled();
      expect(screen.getByText(/acme corp/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/1 allocation.*pending/i)).toBeInTheDocument();
  });

  it('approves corporate allocation', async () => {
    const user = userEvent.setup();
    mockGetVendorAllocations.mockResolvedValue({
      success: true,
      data: [
        {
          id: 'alloc-1',
          companyName: 'Acme Corp',
          parkingSpaceTitle: 'Corporate Lot',
          status: 0,
          totalSlots: 5,
          fixedSlots: 2,
          sharedSlots: 3,
          monthlyRate: 10000,
          startDate: '2026-08-01',
          endDate: '2026-08-31',
        },
      ],
    });
    mockApproveAllocation.mockResolvedValue({ success: true });

    renderVendorBookings();
    await waitFor(() => expect(mockGetVendorBookings).toHaveBeenCalled());
    await user.selectOptions(screen.getByRole('combobox'), 'allocations');
    await waitFor(() => expect(screen.getByText(/acme corp/i)).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /approve allocation/i }));

    await waitFor(() => {
      expect(mockApproveAllocation).toHaveBeenCalledWith('alloc-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Corporate allocation approved successfully!');
    });
  });

  it('toasts on load failure', async () => {
    mockGetVendorBookings.mockRejectedValue(new Error('network'));

    renderVendorBookings();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });
  });

  it('subscribes to notification refresh triggers', async () => {
    renderVendorBookings();
    await waitFor(() => {
      expect(mockSubscribeToRefresh).toHaveBeenCalledWith(
        'VendorBookings',
        expect.arrayContaining(['booking.requested', 'extension.requested']),
        expect.any(Function)
      );
    });
  });
});
