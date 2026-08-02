import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import LeaseBrowse from './LeaseBrowse';

const mockNavigate = vi.fn();
const mockSearchParking = vi.fn();
const mockGetParkingById = vi.fn();
const mockRequestAllocation = vi.fn();
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

let authState = {
  companyRole: 'Admin',
  isolationEnabled: true,
};

let companyState = {
  isCorporateMode: true,
  activeCompanyId: 'co-1',
};

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => authState,
}));

vi.mock('../../contexts/CompanyContext', () => ({
  useCompany: () => companyState,
}));

vi.mock('../../services/api', () => ({
  default: {
    searchParking: (...a) => mockSearchParking(...a),
    getParkingById: (...a) => mockGetParkingById(...a),
  },
}));

vi.mock('../../services/corporateService', () => ({
  default: {
    requestAllocation: (...a) => mockRequestAllocation(...a),
  },
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../../utils/toast.jsx', () => ({
  default: {
    success: (...a) => mockToastSuccess(...a),
    error: (...a) => mockToastError(...a),
  },
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <LeaseBrowse />
    </MemoryRouter>
  );
}

describe('LeaseBrowse (PR8 / KD-17)', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockSearchParking.mockReset();
    mockGetParkingById.mockReset();
    mockRequestAllocation.mockReset();
    mockToastSuccess.mockReset();
    mockToastError.mockReset();
    authState = { companyRole: 'Admin', isolationEnabled: true };
    companyState = { isCorporateMode: true, activeCompanyId: 'co-1' };
    mockSearchParking.mockResolvedValue({
      success: true,
      data: {
        parkingSpaces: [
          {
            id: 'lot-1',
            title: 'Downtown Lot',
            city: 'Mumbai',
            address: '1 Main St',
            totalSpots: 20,
            hourlyRate: 50,
          },
        ],
        totalPages: 1,
      },
    });
  });

  afterEach(() => {
    cleanup();
  });

  it('redirects when not in corporate mode', async () => {
    companyState = { isCorporateMode: false, activeCompanyId: null };
    renderPage();
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard', { replace: true });
    });
  });

  it('denies non-admin company roles', async () => {
    authState = { companyRole: 'Employee', isolationEnabled: true };
    renderPage();
    expect(await screen.findByText(/only company admins/i)).toBeInTheDocument();
    expect(mockSearchParking).not.toHaveBeenCalled();
  });

  it('searches vendor lots for company Admin', async () => {
    renderPage();
    expect(await screen.findByText('Downtown Lot')).toBeInTheDocument();
    await waitFor(() => {
      expect(mockSearchParking).toHaveBeenCalled();
    });
    expect(screen.getByRole('heading', { name: /lease browse/i })).toBeInTheDocument();
    expect(screen.queryByText(/request booking/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/pay ₹/i)).not.toBeInTheDocument();
  });

  it('opens lot detail and submits allocation request only', async () => {
    const user = userEvent.setup();
    mockGetParkingById.mockResolvedValue({
      success: true,
      data: {
        id: 'lot-1',
        title: 'Downtown Lot',
        city: 'Mumbai',
        address: '1 Main St',
        totalSpots: 20,
      },
    });
    mockRequestAllocation.mockResolvedValue({ success: true, data: { id: 'alloc-1' } });

    renderPage();
    expect(await screen.findByText('Downtown Lot')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /downtown lot/i }));

    expect(await screen.findByRole('heading', { name: /request corporate allocation/i })).toBeInTheDocument();
    expect(screen.getByText(/allocation request only/i)).toBeInTheDocument();
    expect(screen.getByText(/combined 2w \+ 4w/i)).toBeInTheDocument();
    expect(screen.getByText(/4-wheeler/i)).toBeInTheDocument();
    expect(screen.getByText(/2-wheeler/i)).toBeInTheDocument();

    // Dual pool totals: 4W then 2W (ids lb-4w-total / lb-2w-total)
    const fourTotal = screen.getByLabelText((_, el) => el?.id === 'lb-4w-total');
    const twoTotal = screen.getByLabelText((_, el) => el?.id === 'lb-2w-total');
    await user.clear(fourTotal);
    await user.type(fourTotal, '12');
    await user.clear(twoTotal);
    await user.type(twoTotal, '5');

    await user.type(screen.getByLabelText(/start date/i), '2026-09-01');
    await user.type(screen.getByLabelText(/end date/i), '2026-12-01');
    await user.click(screen.getByRole('button', { name: /submit lease request/i }));

    await waitFor(() => {
      expect(mockRequestAllocation).toHaveBeenCalled();
    });
    const payload = mockRequestAllocation.mock.calls[0][0];
    expect(payload.parkingSpaceId).toBe('lot-1');
    expect(payload.fourWheeler).toEqual(expect.objectContaining({ totalSlots: 12 }));
    expect(payload.twoWheeler).toEqual(expect.objectContaining({ totalSlots: 5 }));
    expect(payload.policy).toBeTruthy();
    expect(mockToastSuccess).toHaveBeenCalled();
    expect(mockNavigate).toHaveBeenCalledWith('/corporate/allocations');
  });

  it('blocks dual-pool request when combined exceeds facility capacity', async () => {
    const user = userEvent.setup();
    mockGetParkingById.mockResolvedValue({
      success: true,
      data: {
        id: 'lot-1',
        title: 'Downtown Lot',
        city: 'Mumbai',
        address: '1 Main St',
        totalSpots: 10,
      },
    });

    renderPage();
    expect(await screen.findByText('Downtown Lot')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /downtown lot/i }));
    await screen.findByRole('heading', { name: /request corporate allocation/i });

    const fourTotal = screen.getByLabelText((_, el) => el?.id === 'lb-4w-total');
    const twoTotal = screen.getByLabelText((_, el) => el?.id === 'lb-2w-total');
    await user.clear(fourTotal);
    await user.type(fourTotal, '8');
    await user.clear(twoTotal);
    await user.type(twoTotal, '5');
    await user.type(screen.getByLabelText(/start date/i), '2026-09-01');
    await user.type(screen.getByLabelText(/end date/i), '2026-12-01');
    await user.click(screen.getByRole('button', { name: /submit lease request/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(expect.stringMatching(/cannot exceed facility capacity/i));
    });
    expect(mockRequestAllocation).not.toHaveBeenCalled();
  });

  it('runs city search from the form', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Downtown Lot');

    mockSearchParking.mockClear();
    mockSearchParking.mockResolvedValue({
      success: true,
      data: { parkingSpaces: [], totalPages: 1 },
    });

    await user.type(screen.getByLabelText(/city/i), 'Pune');
    await user.click(screen.getByRole('button', { name: /search vendor lots/i }));

    await waitFor(() => {
      expect(mockSearchParking).toHaveBeenCalledWith(
        expect.objectContaining({ city: 'Pune', page: 1 })
      );
    });
  });
});
