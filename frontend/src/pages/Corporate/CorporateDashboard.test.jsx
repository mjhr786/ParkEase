import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import CorporateDashboard from './CorporateDashboard';
import { ThemeProvider } from '../../contexts/ThemeContext';

const mockNavigate = vi.fn();
const mockGetDashboard = vi.fn();
const mockExportDashboard = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let companyState = {
  activeCompanyId: 'co-1',
  companyDetails: { name: 'Acme Corp' },
  isCorporateMode: true,
};

vi.mock('../../contexts/CompanyContext', () => ({
  useCompany: () => companyState,
}));

vi.mock('../../services/corporateService', () => ({
  default: {
    getDashboard: (...args) => mockGetDashboard(...args),
    exportDashboard: (...args) => mockExportDashboard(...args),
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
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }) => <div data-testid="chart">{children}</div>,
  LineChart: ({ children }) => <div>{children}</div>,
  BarChart: ({ children }) => <div>{children}</div>,
  Line: () => null,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
}));

const sampleStats = {
  activeMembers: 5,
  totalMembers: 10,
  activeAllocations: 3,
  totalAllocations: 8,
  ownedParkingSpaces: 2,
  ownedParkingSlots: 20,
  leasedAllocations: 1,
  pendingVendorAllocations: 0,
  activeWaitlistEntries: 1,
  expiringAllocationsWithin30Days: 0,
  totalBookingsThisMonth: 12,
  visitorBookingsThisMonth: 2,
  totalHoursUsedThisMonth: 40.5,
  monthlySpend: 15000,
  utilizationPercentage: 72,
  suspiciousActivityCount: 0,
  bookingsByDay: [{ label: 'Mon', volume: 2 }],
  allocationBreakdown: [{ parkingSpaceTitle: 'Lot A', utilizationPercent: 50 }],
};

function renderDashboard() {
  return render(
    <ThemeProvider>
      <MemoryRouter>
        <CorporateDashboard />
      </MemoryRouter>
    </ThemeProvider>
  );
}

describe('CorporateDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    companyState = {
      activeCompanyId: 'co-1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
    };
    mockGetDashboard.mockResolvedValue({ success: true, data: sampleStats });
  });

  afterEach(() => {
    cleanup();
  });

  it('redirects when not in corporate mode', async () => {
    companyState = { ...companyState, isCorporateMode: false };
    renderDashboard();
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard', { replace: true });
    });
  });

  it('shows loading spinner then dashboard stats', async () => {
    renderDashboard();
    expect(document.querySelector('.spinner')).toBeTruthy();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /acme corp dashboard/i })).toBeInTheDocument();
    });
    expect(screen.getByText('Active Members')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText(/out of 10 total/i)).toBeInTheDocument();
    expect(screen.getByText('Bookings This Month')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText(/Booking Trend/i)).toBeInTheDocument();
  });

  it('shows no-data state when dashboard returns empty', async () => {
    mockGetDashboard.mockResolvedValue({ success: true, data: null });
    renderDashboard();
    await waitFor(() => {
      expect(screen.getByText(/no data available/i)).toBeInTheDocument();
    });
  });

  it('toasts on API failure', async () => {
    mockGetDashboard.mockResolvedValue({ success: false, message: 'Denied' });
    renderDashboard();
    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Denied');
    });
  });

  it('toasts when server is unreachable', async () => {
    mockGetDashboard.mockRejectedValue(new Error('offline'));
    renderDashboard();
    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Could not reach server');
    });
  });

  it('navigates to bookings', async () => {
    const user = userEvent.setup();
    renderDashboard();
    await waitFor(() => expect(screen.getByRole('heading', { name: /acme corp/i })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /view bookings/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/corporate/bookings');
  });

  it('exports dashboard CSV', async () => {
    const user = userEvent.setup();
    const blob = new Blob(['a,b'], { type: 'text/csv' });
    mockExportDashboard.mockResolvedValue({ blob, fileName: 'dash.csv' });

    const createObjectURL = vi.fn(() => 'blob:mock');
    const revokeObjectURL = vi.fn();
    global.URL.createObjectURL = createObjectURL;
    global.URL.revokeObjectURL = revokeObjectURL;

    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    renderDashboard();
    await waitFor(() => expect(screen.getByRole('button', { name: /export csv/i })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /export csv/i }));

    await waitFor(() => {
      expect(mockExportDashboard).toHaveBeenCalled();
      expect(mockToastSuccess).toHaveBeenCalledWith('Dashboard CSV downloaded');
      expect(createObjectURL).toHaveBeenCalledWith(blob);
    });

    clickSpy.mockRestore();
  });

  it('toasts when export fails', async () => {
    const user = userEvent.setup();
    mockExportDashboard.mockRejectedValue(new Error('export broken'));

    renderDashboard();
    await waitFor(() => expect(screen.getByRole('button', { name: /export csv/i })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /export csv/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('export broken');
    });
  });
});
