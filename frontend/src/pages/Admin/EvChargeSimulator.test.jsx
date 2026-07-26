import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import EvChargeSimulator from './EvChargeSimulator';

const mockNavigate = vi.fn();
const mockSimulateEv = vi.fn();
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
    simulateEvChargingSession: (...args) => mockSimulateEv(...args),
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
      <EvChargeSimulator />
    </MemoryRouter>
  );
}

function submitForm() {
  fireEvent.submit(document.querySelector('form'));
}

describe('EvChargeSimulator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAuthenticated: true, isAdmin: false, loading: false };
  });

  afterEach(() => {
    cleanup();
  });

  it('redirects unauthenticated users', () => {
    authState = { isAuthenticated: false, isAdmin: false, loading: false };
    const { container } = renderSim();
    expect(mockNavigate).toHaveBeenCalledWith('/login');
    expect(container.firstChild).toBeNull();
  });

  it('renders heading and form fields', () => {
    renderSim();
    expect(screen.getByRole('heading', { name: /ev charge simulator/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/guid from my bookings/i)).toBeInTheDocument();
    expect(screen.getByDisplayValue('12.5')).toBeInTheDocument();
    expect(screen.getByDisplayValue('MOCK-1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /simulate full charge session/i })).toBeInTheDocument();
  });

  it('toasts when booking id is empty', () => {
    renderSim();
    submitForm();
    expect(mockToastError).toHaveBeenCalledWith('Booking id is required');
    expect(mockSimulateEv).not.toHaveBeenCalled();
  });

  it('toasts when energy is not positive', () => {
    renderSim();
    fireEvent.change(screen.getByPlaceholderText(/guid from my bookings/i), {
      target: { value: 'booking-1' },
    });
    fireEvent.change(screen.getByDisplayValue('12.5'), { target: { value: '0' } });
    submitForm();

    expect(mockToastError).toHaveBeenCalledWith('Energy kWh must be positive');
    expect(mockSimulateEv).not.toHaveBeenCalled();
  });

  it('shows success result card', async () => {
    mockSimulateEv.mockResolvedValue({
      success: true,
      message: 'Charge completed',
      data: {
        status: 'Completed',
        ocppTransactionId: 'txn-99',
        energyDeliveredKwh: 12.5,
        ratePerKwh: 15,
        energyFeeAmount: 187.5,
      },
    });

    renderSim();
    fireEvent.change(screen.getByPlaceholderText(/guid from my bookings/i), {
      target: { value: '  booking-abc  ' },
    });
    submitForm();

    await waitFor(() => {
      expect(mockSimulateEv).toHaveBeenCalledWith({
        bookingId: 'booking-abc',
        energyKwh: 12.5,
        stationId: 'MOCK-1',
      });
      expect(mockToastSuccess).toHaveBeenCalledWith('Charge completed');
    });

    expect(screen.getByText('Completed')).toBeInTheDocument();
    expect(screen.getByText('txn-99')).toBeInTheDocument();
    expect(screen.getByText(/12\.5 kWh/)).toBeInTheDocument();
    expect(screen.getByText(/₹187\.5/)).toBeInTheDocument();
  });

  it('toasts API failure message', async () => {
    mockSimulateEv.mockResolvedValue({
      success: false,
      message: 'Booking not eligible',
    });

    renderSim();
    fireEvent.change(screen.getByPlaceholderText(/guid from my bookings/i), {
      target: { value: 'booking-x' },
    });
    submitForm();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Booking not eligible');
    });
  });

  it('toasts on network error', async () => {
    mockSimulateEv.mockRejectedValue(new Error('offline'));

    renderSim();
    fireEvent.change(screen.getByPlaceholderText(/guid from my bookings/i), {
      target: { value: 'booking-y' },
    });
    submitForm();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('offline');
    });
  });

  it('shows admin copy when user is admin', () => {
    authState = { isAuthenticated: true, isAdmin: true, loading: false };
    renderSim();
    expect(screen.getByText(/admin can simulate any booking/i)).toBeInTheDocument();
  });
});
