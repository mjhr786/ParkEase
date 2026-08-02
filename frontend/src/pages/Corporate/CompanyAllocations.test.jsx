import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import CompanyAllocations from './CompanyAllocations';

const mockNavigate = vi.fn();
const mockGetAllocations = vi.fn();
const mockGetMembers = vi.fn();
const mockGetWaitlist = vi.fn();
const mockAssignFixedSlot = vi.fn();
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
    getAllocations: (...a) => mockGetAllocations(...a),
    getMembers: (...a) => mockGetMembers(...a),
    getWaitlist: (...a) => mockGetWaitlist(...a),
    assignFixedSlot: (...a) => mockAssignFixedSlot(...a),
    updatePolicy: vi.fn(),
    updateContract: vi.fn(),
    removeFixedSlot: vi.fn(),
    bookParking: vi.fn(),
    bookVisitor: vi.fn(),
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
      <CompanyAllocations />
    </MemoryRouter>
  );
}

describe('CompanyAllocations dual pools', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockGetAllocations.mockReset();
    mockGetMembers.mockReset();
    mockGetWaitlist.mockReset();
    mockAssignFixedSlot.mockReset();
    mockToastError.mockReset();
    mockToastSuccess.mockReset();
    companyState = { isCorporateMode: true, activeCompanyId: 'co-1' };
    mockGetMembers.mockResolvedValue({
      success: true,
      data: { members: [{ id: 'mem-1', userName: 'Alice', userEmail: 'a@x.com', isActive: true }] },
    });
    mockGetWaitlist.mockResolvedValue({ success: true, data: [] });
    mockGetAllocations.mockResolvedValue({
      success: true,
      data: [
        {
          id: 'alloc-1',
          parkingSpaceTitle: 'Dual Bay Lot',
          totalSlots: 30,
          fixedSlots: 3,
          sharedSlots: 27,
          monthlyRate: 0,
          status: 1,
          sourceType: 1,
          leaseReference: null,
          fourWheeler: { totalSlots: 20, fixedSlots: 2, sharedSlots: 18 },
          twoWheeler: { totalSlots: 10, fixedSlots: 1, sharedSlots: 9 },
          policy: {
            maxBookingsPerEmployeePerDay: 1,
            maxBookingsPerEmployeePerWeek: 5,
            priorityThreshold: 1,
            allowedStartTime: '07:00:00',
            allowedEndTime: '22:00:00',
            allowWeekends: false,
          },
          fixedAssignments: [
            { membershipId: 'mem-1', slotNumber: 1, vehicleClass: 1, userName: 'Alice' },
            { membershipId: 'mem-1', slotNumber: 1, vehicleClass: 2, userName: 'Alice' },
          ],
          startDate: '2026-07-01T00:00:00Z',
          endDate: '2026-12-31T00:00:00Z',
        },
      ],
    });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders per-class 4W and 2W capacity summary', async () => {
    renderPage();
    expect(await screen.findByText('Dual Bay Lot')).toBeInTheDocument();
    expect(screen.getByText(/4W:\s*20/)).toBeInTheDocument();
    expect(screen.getByText(/2W:\s*10/)).toBeInTheDocument();
    expect(screen.getByText(/30 Slots/i)).toBeInTheDocument();
  });

  it('shows fixed assignment vehicle class labels 2W and 4W', async () => {
    renderPage();
    expect(await screen.findByText('Dual Bay Lot')).toBeInTheDocument();
    expect(screen.getByText(/2W Slot/i)).toBeInTheDocument();
    expect(screen.getByText(/4W Slot/i)).toBeInTheDocument();
  });

  it('assign fixed slot modal includes vehicle class selector', async () => {
    const user = userEvent.setup();
    renderPage();
    expect(await screen.findByText('Dual Bay Lot')).toBeInTheDocument();

    // Link-style control under Fixed Assignments header
    await user.click(screen.getByRole('button', { name: /^assign slot$/i }));

    expect(await screen.findByText(/vehicle class/i)).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /4-wheeler/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /2-wheeler/i })).toBeInTheDocument();
  });
});
