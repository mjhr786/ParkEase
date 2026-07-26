import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import LprSimulator from './LprSimulator';

const mockNavigate = vi.fn();
const mockGetMyListings = vi.fn();
const mockSimulateLpr = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let authState = { isAuthenticated: true, isAdmin: false, loading: false };

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => authState,
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../../services/api', () => ({
  default: {
    getMyListings: (...args) => mockGetMyListings(...args),
    simulateLprEvent: (...args) => mockSimulateLpr(...args),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

function renderSim() {
  return render(
    <MemoryRouter>
      <LprSimulator />
    </MemoryRouter>
  );
}

const lprListing = {
  id: 'space-1',
  title: 'LPR Lot A',
  city: 'Pune',
  isLprEnabled: true,
};

describe('LprSimulator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAuthenticated: true, isAdmin: false, loading: false };
    mockGetMyListings.mockResolvedValue({ success: true, data: [lprListing] });
  });

  afterEach(() => {
    cleanup();
  });

  it('redirects unauthenticated users', async () => {
    authState = { isAuthenticated: false, isAdmin: false, loading: false };
    renderSim();
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/login');
    });
  });

  it('loads LPR lots and auto-selects single lot', async () => {
    renderSim();

    await waitFor(() => {
      expect(mockGetMyListings).toHaveBeenCalled();
    });

    expect(screen.getByRole('heading', { name: /lpr simulator/i })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('option', { name: /LPR Lot A/i })).toBeInTheDocument();
    });
    // single lot auto-selected on the facility <select>
    expect(screen.getByDisplayValue(/LPR Lot A/i)).toBeInTheDocument();
  });

  it('shows empty LPR message and manual GUID when no LPR lots', async () => {
    mockGetMyListings.mockResolvedValue({
      success: true,
      data: [{ id: 'x', title: 'No LPR', isLprEnabled: false }],
    });

    renderSim();

    await waitFor(() => {
      expect(screen.getByText(/no lpr-enabled listings found/i)).toBeInTheDocument();
    });
    expect(screen.getByPlaceholderText(/guid of the parking facility/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /enable lpr on a listing/i })).toHaveAttribute(
      'href',
      '/my/listings'
    );
  });

  it('does not call API when required fields are empty (native + JS validation)', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({ success: true, data: [] });

    renderSim();
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/guid of the parking facility/i)).toBeInTheDocument();
    });

    // HTML5 required may block submit; either way the API must not run
    await user.click(screen.getByRole('button', { name: /simulate lpr event/i }));
    expect(mockSimulateLpr).not.toHaveBeenCalled();
  });

  it('simulates entry and shows access granted', async () => {
    const user = userEvent.setup();
    mockSimulateLpr.mockResolvedValue({
      success: true,
      message: 'Gate opened',
      data: {
        accessGranted: true,
        decision: 'Granted',
        licensePlateNormalized: 'KA01AB1234',
        direction: 'Entry',
        bookingReference: 'BK-100',
        bookingId: 'b-1',
        attemptId: 'att-1',
      },
    });

    renderSim();
    await waitFor(() => {
      expect(screen.getByRole('option', { name: /LPR Lot A/i })).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText(/KA01AB1234/i), 'KA01AB1234');
    await user.click(screen.getByRole('button', { name: /simulate lpr event/i }));

    await waitFor(() => {
      expect(mockSimulateLpr).toHaveBeenCalledWith({
        licensePlate: 'KA01AB1234',
        parkingSpaceId: 'space-1',
        direction: 'Entry',
      });
      expect(mockToastSuccess).toHaveBeenCalledWith('Gate opened');
    });

    expect(screen.getByText(/access granted/i)).toBeInTheDocument();
    expect(screen.getByText('BK-100')).toBeInTheDocument();
    expect(screen.getByText('Granted')).toBeInTheDocument();
  });

  it('shows access denied result', async () => {
    const user = userEvent.setup();
    mockSimulateLpr.mockResolvedValue({
      success: true,
      data: {
        accessGranted: false,
        decision: 'Denied',
        licensePlateNormalized: 'MH12XX0000',
        direction: 'Entry',
        denialReasonCode: 'NoBooking',
        denialMessage: 'No active booking',
      },
    });

    renderSim();
    await waitFor(() => {
      expect(screen.getByRole('option', { name: /LPR Lot A/i })).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText(/KA01AB1234/i), 'MH12XX0000');
    await user.click(screen.getByRole('button', { name: /simulate lpr event/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('No active booking');
    });
    expect(screen.getByText(/access denied/i)).toBeInTheDocument();
    expect(screen.getByText('NoBooking')).toBeInTheDocument();
  });

  it('toasts on network error', async () => {
    const user = userEvent.setup();
    mockSimulateLpr.mockRejectedValue(new Error('timeout'));

    renderSim();
    await waitFor(() => {
      expect(screen.getByRole('option', { name: /LPR Lot A/i })).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText(/KA01AB1234/i), 'KA01ZZ9999');
    await user.click(screen.getByRole('button', { name: /simulate lpr event/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('timeout');
    });
  });

  it('allows switching to manual facility ID', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({
      success: true,
      data: [
        lprListing,
        { id: 'space-2', title: 'LPR Lot B', city: 'Mumbai', isLprEnabled: true },
      ],
    });

    renderSim();
    await waitFor(() => {
      expect(screen.getByText(/enter facility id manually/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /enter facility id manually/i }));
    expect(screen.getByPlaceholderText(/guid of the parking facility/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /choose from my lpr lots/i }));
    expect(screen.getByRole('option', { name: /LPR Lot B/i })).toBeInTheDocument();
  });
});
