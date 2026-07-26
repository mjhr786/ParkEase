import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import AccessPassScanner from './AccessPassScanner';

const mockVerifyAccessPass = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

vi.mock('../../services/api', () => ({
  default: {
    verifyAccessPass: (...args) => mockVerifyAccessPass(...args),
  },
}));

vi.mock('../../utils/toast.jsx', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

function renderScanner() {
  return render(
    <MemoryRouter>
      <AccessPassScanner />
    </MemoryRouter>
  );
}

describe('AccessPassScanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('renders heading, token field, and inbox link', () => {
    renderScanner();
    expect(screen.getByRole('heading', { name: /scan access pass/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/PE-BK/i)).toBeInTheDocument();
    expect(screen.getByText(/access token/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to inbox/i })).toHaveAttribute('href', '/my/requests');
  });

  it('toasts error when token is empty', async () => {
    const user = userEvent.setup();
    renderScanner();

    await user.click(screen.getByRole('button', { name: /verify pass/i }));

    expect(mockToastError).toHaveBeenCalledWith('Paste or type an access-pass token');
    expect(mockVerifyAccessPass).not.toHaveBeenCalled();
  });

  it('shows access granted result', async () => {
    const user = userEvent.setup();
    mockVerifyAccessPass.mockResolvedValue({
      success: true,
      data: {
        accessGranted: true,
        decision: 'Granted',
        bookingReference: 'BK-200',
        parkingSpaceTitle: 'Downtown Lot',
        status: 1,
        vehicleNumber: 'MH12XY9999',
        startDateTime: '2026-07-26T10:00:00Z',
        endDateTime: '2026-07-26T12:00:00Z',
      },
    });

    renderScanner();
    await user.type(screen.getByPlaceholderText(/PE-BK/i), 'PE-BK-TOKEN');
    await user.click(screen.getByRole('button', { name: /verify pass/i }));

    await waitFor(() => {
      expect(mockVerifyAccessPass).toHaveBeenCalledWith('PE-BK-TOKEN');
      expect(mockToastSuccess).toHaveBeenCalledWith('Access granted');
    });
    expect(screen.getByText(/access granted/i)).toBeInTheDocument();
    expect(screen.getByText('BK-200')).toBeInTheDocument();
    expect(screen.getByText('Downtown Lot')).toBeInTheDocument();
    expect(screen.getByText('MH12XY9999')).toBeInTheDocument();
    expect(screen.getByText('Confirmed')).toBeInTheDocument();
  });

  it('shows access denied result with reason', async () => {
    const user = userEvent.setup();
    mockVerifyAccessPass.mockResolvedValue({
      success: true,
      data: {
        accessGranted: false,
        decision: 'Denied',
        denialMessage: 'Pass expired',
        denialReasonCode: 'OutsideWindow',
        bookingReference: 'BK-201',
      },
    });

    renderScanner();
    await user.type(screen.getByPlaceholderText(/PE-BK/i), '  PE-EXPIRED  ');
    await user.click(screen.getByRole('button', { name: /verify pass/i }));

    await waitFor(() => {
      expect(mockVerifyAccessPass).toHaveBeenCalledWith('PE-EXPIRED');
      expect(mockToastError).toHaveBeenCalledWith('Pass expired');
    });
    expect(screen.getByText(/access denied/i)).toBeInTheDocument();
    expect(screen.getByText('Pass expired')).toBeInTheDocument();
    expect(screen.getByText('OutsideWindow')).toBeInTheDocument();
  });

  it('toasts when API returns failure without data', async () => {
    const user = userEvent.setup();
    mockVerifyAccessPass.mockResolvedValue({
      success: false,
      message: 'Invalid token format',
    });

    renderScanner();
    await user.type(screen.getByPlaceholderText(/PE-BK/i), 'bad');
    await user.click(screen.getByRole('button', { name: /verify pass/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Invalid token format');
    });
  });

  it('toasts on network error', async () => {
    const user = userEvent.setup();
    mockVerifyAccessPass.mockRejectedValue(new Error('network down'));

    renderScanner();
    await user.type(screen.getByPlaceholderText(/PE-BK/i), 'PE-X');
    await user.click(screen.getByRole('button', { name: /verify pass/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });
  });
});
