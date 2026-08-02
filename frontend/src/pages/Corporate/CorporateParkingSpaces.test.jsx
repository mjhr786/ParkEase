import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import CorporateParkingSpaces from './CorporateParkingSpaces';

const mockNavigate = vi.fn();
const mockGetParkingSpaces = vi.fn();
const mockCreateOwnedAllocation = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let companyState = {
  isCorporateMode: true,
  activeCompanyId: 'co-1',
};

vi.mock('../../contexts/CompanyContext', () => ({
  useCompany: () => companyState,
}));

vi.mock('../../services/corporateService', () => ({
  default: {
    getParkingSpaces: (...a) => mockGetParkingSpaces(...a),
    createOwnedAllocation: (...a) => mockCreateOwnedAllocation(...a),
    createParkingSpace: vi.fn(),
    updateParkingSpace: vi.fn(),
    toggleParkingSpace: vi.fn(),
    retireParkingSpace: vi.fn(),
  },
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('react-hot-toast', () => ({
  default: {
    success: (...a) => mockToastSuccess(...a),
    error: (...a) => mockToastError(...a),
  },
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <CorporateParkingSpaces />
    </MemoryRouter>
  );
}

describe('CorporateParkingSpaces dual pools', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockGetParkingSpaces.mockReset();
    mockCreateOwnedAllocation.mockReset();
    mockToastError.mockReset();
    mockToastSuccess.mockReset();
    companyState = { isCorporateMode: true, activeCompanyId: 'co-1' };
    mockGetParkingSpaces.mockResolvedValue({
      success: true,
      data: [
        {
          id: 'space-1',
          title: 'HQ Owned Lot',
          address: '1 Main',
          city: 'Mumbai',
          totalSpots: 30,
          isActive: true,
          monthlyRate: 0,
        },
      ],
    });
    mockCreateOwnedAllocation.mockResolvedValue({ success: true, data: { id: 'alloc-1' } });
  });

  afterEach(() => {
    cleanup();
  });

  it('opens allocate modal with 4W and 2W pool fields', async () => {
    const user = userEvent.setup();
    renderPage();
    expect(await screen.findByText('HQ Owned Lot')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^allocate$/i }));

    expect(await screen.findByRole('heading', { name: /activate internal allocation/i })).toBeInTheDocument();
    expect(screen.getByText(/4-wheeler/i)).toBeInTheDocument();
    expect(screen.getByText(/2-wheeler/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/4w total/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/2w total/i)).toBeInTheDocument();
  });

  it('submits dual-pool payload on activate', async () => {
    const user = userEvent.setup();
    renderPage();
    expect(await screen.findByText('HQ Owned Lot')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^allocate$/i }));
    await screen.findByRole('heading', { name: /activate internal allocation/i });

    const fourTotal = screen.getByLabelText(/4w total/i);
    const twoTotal = screen.getByLabelText(/2w total/i);
    await user.clear(fourTotal);
    await user.type(fourTotal, '20');
    await user.clear(twoTotal);
    await user.type(twoTotal, '10');

    // dates required
    const start = screen.getByLabelText(/start date/i);
    const end = screen.getByLabelText(/end date/i);
    await user.clear(start);
    await user.type(start, '2026-07-01');
    await user.clear(end);
    await user.type(end, '2026-12-31');

    await user.click(screen.getByRole('button', { name: /activate allocation/i }));

    await waitFor(() => {
      expect(mockCreateOwnedAllocation).toHaveBeenCalled();
    });
    const [spaceId, payload] = mockCreateOwnedAllocation.mock.calls[0];
    expect(spaceId).toBe('space-1');
    expect(payload.fourWheeler.totalSlots).toBe(20);
    expect(payload.twoWheeler.totalSlots).toBe(10);
    expect(mockToastSuccess).toHaveBeenCalled();
  });

  it('rejects zero capacity on both pools', async () => {
    const user = userEvent.setup();
    renderPage();
    expect(await screen.findByText('HQ Owned Lot')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^allocate$/i }));
    await screen.findByRole('heading', { name: /activate internal allocation/i });

    await user.clear(screen.getByLabelText(/4w total/i));
    await user.type(screen.getByLabelText(/4w total/i), '0');
    await user.clear(screen.getByLabelText(/2w total/i));
    await user.type(screen.getByLabelText(/2w total/i), '0');

    await user.click(screen.getByRole('button', { name: /activate allocation/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        expect.stringMatching(/at least one vehicle class pool/i)
      );
    });
    expect(mockCreateOwnedAllocation).not.toHaveBeenCalled();
  });
});
