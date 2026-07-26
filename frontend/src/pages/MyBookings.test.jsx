import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import MyBookings from './MyBookings';

const mockGetMyBookings = vi.fn();
const mockCancelBooking = vi.fn();
const mockGetStripeConfig = vi.fn();
const mockSubscribeToRefresh = vi.fn(() => () => {});
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

vi.mock('../context/NotificationContext', () => ({
  useNotificationContext: () => ({
    subscribeToRefresh: (...args) => mockSubscribeToRefresh(...args),
  }),
}));

const mockCheckIn = vi.fn();
const mockCheckOut = vi.fn();
const mockGetAccessPass = vi.fn();
const mockRequestExtension = vi.fn();
const mockCalculatePrice = vi.fn();

vi.mock('../services/api', () => ({
  default: {
    getMyBookings: (...args) => mockGetMyBookings(...args),
    cancelBooking: (...args) => mockCancelBooking(...args),
    getStripeConfig: (...args) => mockGetStripeConfig(...args),
    checkIn: (...args) => mockCheckIn(...args),
    checkOut: (...args) => mockCheckOut(...args),
    getAccessPass: (...args) => mockGetAccessPass(...args),
    createPaymentOrder: vi.fn(),
    verifyPayment: vi.fn(),
    createReview: vi.fn(),
    calculatePrice: (...args) => mockCalculatePrice(...args),
    requestExtension: (...args) => mockRequestExtension(...args),
    getParkingById: vi.fn(),
  },
}));

vi.mock('../utils/toast.jsx', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

vi.mock('../components/StripeCheckout', () => ({
  default: () => null,
}));

vi.mock('../components/BookedSlots', () => ({
  default: () => null,
}));

function renderMyBookings(initialEntry = '/bookings') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/bookings" element={<MyBookings />} />
      </Routes>
    </MemoryRouter>
  );
}

const confirmedBooking = {
  id: 'b-1',
  parkingSpaceTitle: 'Airport Lot',
  parkingSpaceAddress: 'Terminal Rd',
  status: 1, // Confirmed
  totalAmount: 200,
  bookingReference: 'BK-100',
  startDateTime: '2026-07-26T10:00:00Z',
  endDateTime: '2026-07-26T12:00:00Z',
  vehicleNumber: 'MH01AB1234',
  pricingType: 0,
  parkingSpaceId: 'ps-1',
};

describe('MyBookings page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSubscribeToRefresh.mockReturnValue(() => {});
    mockGetStripeConfig.mockResolvedValue({ publishableKey: 'pk_test' });
    mockGetMyBookings.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders heading and filter', async () => {
    renderMyBookings();
    expect(screen.getByRole('heading', { name: /my bookings/i })).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toBeInTheDocument();
    await waitFor(() => expect(mockGetMyBookings).toHaveBeenCalled());
  });

  it('shows empty state when no bookings', async () => {
    renderMyBookings();
    await waitFor(() => {
      expect(screen.getByText(/no bookings found/i)).toBeInTheDocument();
    });
    expect(screen.getByRole('link', { name: /find parking/i })).toHaveAttribute('href', '/search');
  });

  it('renders booking cards', async () => {
    mockGetMyBookings.mockResolvedValue({ success: true, data: [confirmedBooking] });

    renderMyBookings();

    await waitFor(() => {
      expect(screen.getByText('Airport Lot')).toBeInTheDocument();
    });
    expect(screen.getByText(/terminal rd/i)).toBeInTheDocument();
    expect(screen.getByText('BK-100')).toBeInTheDocument();
    expect(screen.getByText(/MH01AB1234/i)).toBeInTheDocument();
    expect(document.querySelector('.parking-tag')).toHaveTextContent('Confirmed');
  });

  it('handles paginated bookings response shape', async () => {
    mockGetMyBookings.mockResolvedValue({
      success: true,
      data: { bookings: [confirmedBooking] },
    });

    renderMyBookings();

    await waitFor(() => {
      expect(screen.getByText('Airport Lot')).toBeInTheDocument();
    });
  });

  it('filters by status', async () => {
    const user = userEvent.setup();
    renderMyBookings();
    await waitFor(() => expect(mockGetMyBookings).toHaveBeenCalled());

    mockGetMyBookings.mockClear();
    await user.selectOptions(screen.getByRole('combobox'), '1');

    await waitFor(() => {
      expect(mockGetMyBookings).toHaveBeenCalledWith({ status: '1' });
    });
  });

  it('cancels booking after confirm', async () => {
    const user = userEvent.setup();
    const awaitingPayment = {
      ...confirmedBooking,
      status: 6, // Awaiting Payment — shows Cancel button
    };
    mockGetMyBookings.mockResolvedValue({ success: true, data: [awaitingPayment] });
    mockCancelBooking.mockResolvedValue({ success: true });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderMyBookings();
    await waitFor(() => expect(screen.getByText('Airport Lot')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /^cancel$/i }));

    await waitFor(() => {
      expect(mockCancelBooking).toHaveBeenCalledWith('b-1', 'User requested cancellation');
      expect(mockToastSuccess).toHaveBeenCalledWith('Booking cancelled successfully');
    });
  });

  it('does not cancel when user declines confirm', async () => {
    const user = userEvent.setup();
    mockGetMyBookings.mockResolvedValue({
      success: true,
      data: [{ ...confirmedBooking, status: 6 }],
    });
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderMyBookings();
    await waitFor(() => expect(screen.getByText('Airport Lot')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /^cancel$/i }));
    expect(mockCancelBooking).not.toHaveBeenCalled();
  });

  it('toasts on load failure', async () => {
    mockGetMyBookings.mockRejectedValue(new Error('network'));

    renderMyBookings();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });
  });

  it('subscribes to notification refresh triggers', async () => {
    renderMyBookings();
    await waitFor(() => {
      expect(mockSubscribeToRefresh).toHaveBeenCalledWith(
        'MyBookings',
        expect.arrayContaining(['booking.approved', 'payment.completed']),
        expect.any(Function)
      );
    });
  });

  it('checks in a confirmed booking', async () => {
    const user = userEvent.setup();
    mockGetMyBookings.mockResolvedValue({ success: true, data: [confirmedBooking] });
    mockCheckIn.mockResolvedValue({ success: true });

    renderMyBookings();
    await waitFor(() => expect(screen.getByText('Airport Lot')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /check in/i }));

    await waitFor(() => {
      expect(mockCheckIn).toHaveBeenCalledWith('b-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Checked in successfully');
    });
  });

  it('checks out an in-progress booking', async () => {
    const user = userEvent.setup();
    mockGetMyBookings.mockResolvedValue({
      success: true,
      data: [{ ...confirmedBooking, status: 2 }],
    });
    mockCheckOut.mockResolvedValue({ success: true });

    renderMyBookings();
    await waitFor(() => expect(screen.getByText('Airport Lot')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /check out/i }));

    await waitFor(() => {
      expect(mockCheckOut).toHaveBeenCalledWith('b-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Checked out successfully');
    });
  });

  it('loads access pass for confirmed booking', async () => {
    const user = userEvent.setup();
    mockGetMyBookings.mockResolvedValue({ success: true, data: [confirmedBooking] });
    mockGetAccessPass.mockResolvedValue({
      success: true,
      data: {
        bookingId: 'b-1',
        bookingReference: 'BK-100',
        qrCode: 'PE-BK-100',
        parkingSpaceTitle: 'Airport Lot',
      },
    });

    renderMyBookings();
    await waitFor(() => expect(screen.getByText('Airport Lot')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /access pass/i }));

    await waitFor(() => {
      expect(mockGetAccessPass).toHaveBeenCalledWith('b-1');
    });
  });

  it('shows pending extension banner', async () => {
    mockGetMyBookings.mockResolvedValue({
      success: true,
      data: [
        {
          ...confirmedBooking,
          status: 8,
          pendingExtensionEndDateTime: '2026-07-26T16:00:00Z',
          pendingExtensionAmount: 75,
        },
      ],
    });

    renderMyBookings();

    await waitFor(() => {
      expect(screen.getByText(/extension request pending owner approval/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/amount due if approved: ₹75/i)).toBeInTheDocument();
  });

  it('shows extension payment due action', async () => {
    mockGetMyBookings.mockResolvedValue({
      success: true,
      data: [
        {
          ...confirmedBooking,
          status: 9,
          pendingExtensionAmount: 80,
          pendingExtensionEndDateTime: '2026-07-26T18:00:00Z',
        },
      ],
    });

    renderMyBookings();

    await waitFor(() => {
      expect(screen.getByText(/extension approved — pay/i)).toBeInTheDocument();
    });
    expect(screen.getByRole('button', { name: /pay ₹80/i })).toBeInTheDocument();
  });
});

