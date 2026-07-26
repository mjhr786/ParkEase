import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import EventPackages from './EventPackages';

const mockGetMyListings = vi.fn();
const mockGetMyEventPackages = vi.fn();
const mockGetAnalytics = vi.fn();
const mockCreateEventPackage = vi.fn();
const mockDeactivate = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

vi.mock('../../services/api', () => ({
  default: {
    getMyListings: (...args) => mockGetMyListings(...args),
    getMyEventPackages: (...args) => mockGetMyEventPackages(...args),
    getMyEventPackageAnalytics: (...args) => mockGetAnalytics(...args),
    createEventPackage: (...args) => mockCreateEventPackage(...args),
    deactivateEventPackage: (...args) => mockDeactivate(...args),
  },
}));

vi.mock('../../utils/toast.jsx', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

vi.mock('../../utils/errorHandler', () => ({
  handleApiError: (err, fallback) => err?.message || fallback,
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <EventPackages />
    </MemoryRouter>
  );
}

const listing = { id: 'lot-1', title: 'Stadium Garage' };

const pkg = {
  id: 'pkg-1',
  title: 'VIP Zone',
  zoneName: 'Zone A',
  parkingSpaceTitle: 'Stadium Garage',
  eventName: 'Cup Final',
  venueName: 'Wankhede',
  venueEventId: 've-11111111-2222',
  eventStartUtc: '2026-08-01T18:00:00Z',
  eventEndUtc: '2026-08-01T22:00:00Z',
  accessStartUtc: '2026-08-01T17:00:00Z',
  accessEndUtc: '2026-08-01T23:00:00Z',
  packagePrice: 1500,
  soldCount: 3,
  totalSpots: 20,
  isOnSale: true,
  isActive: true,
};

describe('EventPackages (vendor)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMyListings.mockResolvedValue({ success: true, data: [listing] });
    mockGetMyEventPackages.mockResolvedValue({ success: true, data: [] });
    mockGetAnalytics.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('loads empty state', async () => {
    renderPage();

    await waitFor(() => {
      expect(mockGetMyListings).toHaveBeenCalled();
      expect(mockGetMyEventPackages).toHaveBeenCalled();
      expect(mockGetAnalytics).toHaveBeenCalled();
    });

    expect(screen.getByRole('heading', { name: /event parking packages/i })).toBeInTheDocument();
    expect(screen.getByText(/no event packages yet/i)).toBeInTheDocument();
  });

  it('lists packages and deactivates after confirm', async () => {
    const user = userEvent.setup();
    mockGetMyEventPackages.mockResolvedValue({ success: true, data: [pkg] });
    mockDeactivate.mockResolvedValue({ success: true });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/Zone A · VIP Zone/)).toBeInTheDocument();
    });
    expect(screen.getByText('On sale')).toBeInTheDocument();
    expect(screen.getByText(/Cup Final/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /deactivate/i }));

    await waitFor(() => {
      expect(mockDeactivate).toHaveBeenCalledWith('pkg-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Package deactivated');
    });
  });

  it('toggles create form and validates required fields', async () => {
    const user = userEvent.setup();
    renderPage();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /\+ new package/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /\+ new package/i }));
    expect(screen.getByRole('heading', { name: /create package/i })).toBeInTheDocument();

    // Bypass HTML5 required so handleSubmit toast path runs
    const form = document.querySelector('form');
    fireEvent.submit(form);

    expect(mockToastError).toHaveBeenCalledWith(
      'Facility, title, and event window are required'
    );
    expect(mockCreateEventPackage).not.toHaveBeenCalled();
  });

  it('creates a package successfully', async () => {
    const user = userEvent.setup();
    mockCreateEventPackage.mockResolvedValue({
      success: true,
      data: { venueEventId: 've-aaaaaaaa-bbbb' },
    });
    mockGetMyEventPackages
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValueOnce({ success: true, data: [pkg] });

    renderPage();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /\+ new package/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /\+ new package/i }));
    await user.selectOptions(screen.getByRole('combobox'), 'lot-1');

    // Form order: title, zoneName, eventName, venueName, venueEventId, … then datetime-locals
    const textInputs = screen.getAllByRole('textbox');
    fireEvent.change(textInputs[0], { target: { value: 'Concert Night' } });

    const dateInputs = document.querySelectorAll('input[type="datetime-local"]');
    fireEvent.change(dateInputs[0], { target: { value: '2026-08-15T18:00' } });
    fireEvent.change(dateInputs[1], { target: { value: '2026-08-15T22:00' } });

    fireEvent.submit(document.querySelector('form'));

    await waitFor(() => {
      expect(mockCreateEventPackage).toHaveBeenCalled();
      const payload = mockCreateEventPackage.mock.calls[0][0];
      expect(payload.parkingSpaceId).toBe('lot-1');
      expect(payload.title).toBe('Concert Night');
      expect(payload.packagePrice).toBe(500);
      expect(payload.totalSpots).toBe(20);
      expect(mockToastSuccess).toHaveBeenCalled();
    });
  });

  it('shows analytics and reuses venue event into form', async () => {
    const user = userEvent.setup();
    mockGetAnalytics.mockResolvedValue({
      success: true,
      data: [
        {
          venueEventId: 've-analytics',
          eventName: 'Cup Final',
          venueName: 'Wankhede',
          zoneCount: 2,
          sellThroughPercent: 45,
          soldCount: 9,
          totalSpots: 20,
          grossRevenue: 13500,
          eventStartUtc: '2026-08-01T18:00:00Z',
          eventEndUtc: '2026-08-01T22:00:00Z',
        },
      ],
    });

    renderPage();
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /sell-through analytics/i })).toBeInTheDocument();
      expect(screen.getByText('45% sold')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /add zone to this event/i }));

    expect(mockToastSuccess).toHaveBeenCalledWith(
      expect.stringMatching(/venue event id filled/i)
    );
    expect(screen.getByRole('heading', { name: /create package/i })).toBeInTheDocument();
    expect(screen.getByDisplayValue('ve-analytics')).toBeInTheDocument();
  });

  it('toasts load failure', async () => {
    mockGetMyListings.mockRejectedValue(new Error('network'));

    renderPage();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('network');
    });
  });
});
