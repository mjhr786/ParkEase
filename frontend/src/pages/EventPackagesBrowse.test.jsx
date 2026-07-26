import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import EventPackagesBrowse from './EventPackagesBrowse';

const mockNavigate = vi.fn();
const mockGetVenues = vi.fn();
const mockPurchase = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let authState = { isAuthenticated: true };

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authState,
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../services/api', () => ({
  default: {
    getEventVenuesOnSale: (...args) => mockGetVenues(...args),
    purchaseEventPackage: (...args) => mockPurchase(...args),
  },
}));

vi.mock('../utils/toast.jsx', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

vi.mock('../utils/errorHandler', () => ({
  handleApiError: (err, fallback) => err?.message || fallback,
}));

function renderBrowse() {
  return render(
    <MemoryRouter>
      <EventPackagesBrowse />
    </MemoryRouter>
  );
}

const venue = {
  venueEventId: 've-1',
  eventName: 'Cup Final',
  venueName: 'Wankhede',
  eventStartUtc: '2026-08-01T18:00:00Z',
  eventEndUtc: '2026-08-01T22:00:00Z',
  minPackagePrice: 500,
  maxPackagePrice: 1500,
  totalAvailableSpots: 40,
  zoneCount: 2,
  zones: [
    {
      id: 'pkg-zone-a',
      zoneName: 'VIP Garage',
      title: 'VIP Package',
      packagePrice: 1500,
      parkingSpaceId: 'lot-1',
      parkingSpaceTitle: 'North Lot',
      parkingSpaceCity: 'Mumbai',
      availableSpots: 5,
      totalSpots: 10,
      isOnSale: true,
      earlyEntryMinutes: 30,
      lateExitMinutes: 15,
      eventStartUtc: '2026-08-01T18:00:00Z',
      eventEndUtc: '2026-08-01T22:00:00Z',
    },
  ],
};

describe('EventPackagesBrowse', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAuthenticated: true };
    mockGetVenues.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('shows empty state when no packages on sale', async () => {
    renderBrowse();

    await waitFor(() => {
      expect(mockGetVenues).toHaveBeenCalledWith(50);
    });

    expect(screen.getByRole('heading', { name: /event parking/i })).toBeInTheDocument();
    expect(screen.getByText(/no event packages on sale/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /search regular parking/i })).toHaveAttribute(
      'href',
      '/search'
    );
  });

  it('lists venues and zones', async () => {
    mockGetVenues.mockResolvedValue({ success: true, data: [venue] });
    renderBrowse();

    await waitFor(() => {
      expect(screen.getByText('Cup Final')).toBeInTheDocument();
    });
    expect(screen.getByText('Wankhede')).toBeInTheDocument();
    expect(screen.getByText('VIP Garage')).toBeInTheDocument();
    expect(screen.getByText(/North Lot, Mumbai/)).toBeInTheDocument();
    expect(screen.getByText(/5 left of 10/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /buy zone/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view lot/i })).toHaveAttribute(
      'href',
      '/parking/lot-1'
    );
  });

  it('redirects unauthenticated user to login on purchase', async () => {
    const user = userEvent.setup();
    authState = { isAuthenticated: false };
    mockGetVenues.mockResolvedValue({ success: true, data: [venue] });

    renderBrowse();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /buy zone/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /buy zone/i }));

    expect(mockNavigate).toHaveBeenCalledWith('/login?returnUrl=/events');
    expect(mockPurchase).not.toHaveBeenCalled();
  });

  it('purchases package and navigates to bookings', async () => {
    const user = userEvent.setup();
    mockGetVenues.mockResolvedValue({ success: true, data: [venue] });
    mockPurchase.mockResolvedValue({
      success: true,
      message: 'Package reserved',
    });

    renderBrowse();
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/KA01AB1234/i)).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText(/KA01AB1234/i), 'MH12XY9999');
    await user.click(screen.getByRole('button', { name: /buy zone/i }));

    await waitFor(() => {
      expect(mockPurchase).toHaveBeenCalledWith('pkg-zone-a', {
        vehicleType: 0,
        vehicleNumber: 'MH12XY9999',
      });
      expect(mockToastSuccess).toHaveBeenCalledWith('Package reserved');
      expect(mockNavigate).toHaveBeenCalledWith('/bookings');
    });
  });

  it('toasts purchase failure', async () => {
    const user = userEvent.setup();
    mockGetVenues.mockResolvedValue({ success: true, data: [venue] });
    mockPurchase.mockResolvedValue({
      success: false,
      message: 'Sold out',
    });

    renderBrowse();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /buy zone/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /buy zone/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Sold out');
    });
  });

  it('toasts load failure', async () => {
    mockGetVenues.mockRejectedValue(new Error('load failed'));

    renderBrowse();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('load failed');
    });
  });
});
