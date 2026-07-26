import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import VendorListings from './VendorListings';

const mockGetMyListings = vi.fn();
const mockGetMyAncillary = vi.fn();
const mockGetVendorBookings = vi.fn();
const mockCreateParking = vi.fn();
const mockUpdateParking = vi.fn();
const mockDeleteParking = vi.fn();
const mockCreateAncillary = vi.fn();
const mockUpdateAncillary = vi.fn();
const mockDeactivateAncillary = vi.fn();
const mockSubscribeToRefresh = vi.fn(() => () => {});
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

vi.mock('../context/NotificationContext', () => ({
  useNotificationContext: () => ({
    subscribeToRefresh: (...args) => mockSubscribeToRefresh(...args),
  }),
}));

vi.mock('../services/api', () => ({
  default: {
    getMyListings: (...args) => mockGetMyListings(...args),
    getMyAncillaryServices: (...args) => mockGetMyAncillary(...args),
    getVendorBookings: (...args) => mockGetVendorBookings(...args),
    createParking: (...args) => mockCreateParking(...args),
    updateParking: (...args) => mockUpdateParking(...args),
    deleteParking: (...args) => mockDeleteParking(...args),
    createAncillaryService: (...args) => mockCreateAncillary(...args),
    updateAncillaryService: (...args) => mockUpdateAncillary(...args),
    deactivateAncillaryService: (...args) => mockDeactivateAncillary(...args),
    getPresignedUrl: vi.fn(),
    confirmUpload: vi.fn(),
    deleteParkingFile: vi.fn(),
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

// Avoid Leaflet/DOM map in unit tests
vi.mock('../components/LocationPicker', () => ({
  default: function MockLocationPicker({ onLocationSelect }) {
    return (
      <div data-testid="location-picker">
        <button
          type="button"
          onClick={() => onLocationSelect?.(19.07, 72.87)}
        >
          Pick location
        </button>
      </div>
    );
  },
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <VendorListings />
    </MemoryRouter>
  );
}

const sampleListing = {
  id: 'lot-1',
  title: 'Downtown Garage',
  description: 'Central covered parking',
  address: '12 MG Road',
  city: 'Mumbai',
  state: 'Maharashtra',
  country: 'India',
  postalCode: '400001',
  latitude: 19.07,
  longitude: 72.87,
  parkingType: 2,
  listingCategory: 0,
  instantBook: false,
  totalSpots: 20,
  hourlyRate: 80,
  dailyRate: 500,
  weeklyRate: 3000,
  monthlyRate: 9000,
  is24Hours: true,
  amenities: ['CCTV', 'Security'],
  isActive: true,
  isLprEnabled: true,
  isDynamicPricingEnabled: false,
  hasEvCharging: true,
  isBayGuidanceEnabled: false,
  isValetEnabled: false,
  averageRating: 4.5,
  imageUrls: [],
};

function fillCreateForm(overrides = {}) {
  const values = {
    title: 'New Lot',
    address: '1 Park Ave',
    state: 'Maharashtra',
    city: 'Mumbai',
    postalCode: '400001',
    ...overrides,
  };

  const form = document.querySelector('form');
  expect(form).toBeTruthy();

  const titleInput = form.querySelector('input[type="text"]');
  fireEvent.change(titleInput, { target: { value: values.title } });

  const addressLabel = screen.getByText(/^Address \*$/);
  const addressField = addressLabel.parentElement.querySelector('input');
  fireEvent.change(addressField, { target: { value: values.address } });

  const stateSelect = screen.getByDisplayValue('Select State');
  fireEvent.change(stateSelect, { target: { value: values.state } });

  const citySelect = screen.getByDisplayValue('Select City');
  fireEvent.change(citySelect, { target: { value: values.city } });

  const postal = screen.getByPlaceholderText('6 digits');
  fireEvent.change(postal, { target: { value: values.postalCode } });
}

describe('VendorListings page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // jsdom lacks layout APIs used when opening the create/edit form
    Element.prototype.scrollIntoView = vi.fn();
    mockSubscribeToRefresh.mockReturnValue(() => {});
    mockGetMyListings.mockResolvedValue({ success: true, data: [] });
    mockGetMyAncillary.mockResolvedValue({ success: true, data: [] });
    mockGetVendorBookings.mockResolvedValue({ success: true, data: { bookings: [] } });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders heading and Add Listing control', async () => {
    renderPage();

    expect(screen.getByRole('heading', { name: /my parking listings/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /\+ add listing/i })).toBeInTheDocument();

    await waitFor(() => {
      expect(mockGetMyListings).toHaveBeenCalled();
      expect(mockGetMyAncillary).toHaveBeenCalled();
      expect(mockGetVendorBookings).toHaveBeenCalled();
    });
  });

  it('shows empty state when vendor has no listings', async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/no listings yet/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/create your first parking listing/i)).toBeInTheDocument();
  });

  it('toasts when listings fail to load', async () => {
    mockGetMyListings.mockRejectedValue(new Error('Network down'));

    renderPage();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Network down');
    });
  });

  it('lists parking cards with badges and LPR registry link', async () => {
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText('Downtown Garage')).toBeInTheDocument();
    });

    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('LPR')).toBeInTheDocument();
    expect(screen.getByText(/🔌 EV/)).toBeInTheDocument();
    expect(screen.getByText(/12 MG Road, Mumbai/)).toBeInTheDocument();
    expect(screen.getByText(/20 spots/)).toBeInTheDocument();
    expect(screen.getByText(/₹80/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /lpr registry/i })).toHaveAttribute(
      'href',
      '/my/listings/lot-1/lpr'
    );
    expect(screen.getByRole('link', { name: /view/i })).toHaveAttribute('href', '/parking/lot-1');
  });

  it('toggles create form open and closed', async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(mockGetMyListings).toHaveBeenCalled());

    await user.click(screen.getByRole('button', { name: /\+ add listing/i }));
    expect(screen.getByRole('heading', { name: /create new listing/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create listing/i })).toBeInTheDocument();
    // Lazy LocationPicker resolves under Suspense
    await waitFor(() => {
      expect(screen.getByTestId('location-picker')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /^cancel$/i }));
    expect(screen.queryByRole('heading', { name: /create new listing/i })).not.toBeInTheDocument();
  });

  it('creates a listing after form submit', async () => {
    const user = userEvent.setup();
    mockCreateParking.mockResolvedValue({ success: true });
    mockGetMyListings
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValue({ success: true, data: [{ ...sampleListing, title: 'New Lot' }] });

    renderPage();
    await waitFor(() => expect(mockGetMyListings).toHaveBeenCalled());

    await user.click(screen.getByRole('button', { name: /\+ add listing/i }));
    fillCreateForm();

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /pick location/i })).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: /pick location/i }));

    fireEvent.submit(document.querySelector('form'));

    await waitFor(() => {
      expect(mockCreateParking).toHaveBeenCalled();
    });

    const payload = mockCreateParking.mock.calls[0][0];
    expect(payload.title).toBe('New Lot');
    expect(payload.address).toBe('1 Park Ave');
    expect(payload.state).toBe('Maharashtra');
    expect(payload.city).toBe('Mumbai');
    expect(payload.latitude).toBe(19.07);
    expect(payload.longitude).toBe(72.87);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Listing created successfully!');
    });
  });

  it('toasts when create parking API returns failure', async () => {
    const user = userEvent.setup();
    mockCreateParking.mockResolvedValue({ success: false, message: 'Quota exceeded' });

    renderPage();
    await waitFor(() => expect(mockGetMyListings).toHaveBeenCalled());

    await user.click(screen.getByRole('button', { name: /\+ add listing/i }));
    fillCreateForm();
    fireEvent.submit(document.querySelector('form'));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Quota exceeded');
    });
  });

  it('edits an existing listing via Update Listing', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });
    mockUpdateParking.mockResolvedValue({ success: true });

    renderPage();
    await waitFor(() => expect(screen.getByText('Downtown Garage')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /^edit$/i }));
    expect(screen.getByRole('heading', { name: /edit listing/i })).toBeInTheDocument();
    expect(screen.getByDisplayValue('Downtown Garage')).toBeInTheDocument();

    fireEvent.change(screen.getByDisplayValue('Downtown Garage'), {
      target: { value: 'Downtown Garage Plus' },
    });
    fireEvent.submit(document.querySelector('form'));

    await waitFor(() => {
      expect(mockUpdateParking).toHaveBeenCalledWith(
        'lot-1',
        expect.objectContaining({ title: 'Downtown Garage Plus' })
      );
    });
    expect(mockToastSuccess).toHaveBeenCalledWith('Listing updated successfully!');
  });

  it('deletes a listing after confirm', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });
    mockDeleteParking.mockResolvedValue({ success: true });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderPage();
    await waitFor(() => expect(screen.getByText('Downtown Garage')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(mockDeleteParking).toHaveBeenCalledWith('lot-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Listing deleted successfully!');
    });
  });

  it('does not delete when confirm is cancelled', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderPage();
    await waitFor(() => expect(screen.getByText('Downtown Garage')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /^delete$/i }));
    expect(mockDeleteParking).not.toHaveBeenCalled();
  });

  it('creates an add-on service for a listing', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });
    mockCreateAncillary.mockResolvedValue({ success: true });
    mockGetMyAncillary
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValue({
        success: true,
        data: [{ id: 'svc-1', parkingSpaceId: 'lot-1', name: 'Basic wash', price: 150, isActive: true }],
      });

    renderPage();
    await waitFor(() => expect(screen.getByText('Downtown Garage')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /add-on services/i }));
    expect(screen.getByText(/no add-ons yet/i)).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText(/name \(e\.g\. basic wash\)/i), {
      target: { value: 'Basic wash' },
    });
    fireEvent.change(screen.getByPlaceholderText(/price ₹/i), {
      target: { value: '150' },
    });
    await user.click(screen.getByRole('button', { name: /add service/i }));

    await waitFor(() => {
      expect(mockCreateAncillary).toHaveBeenCalledWith(
        expect.objectContaining({
          parkingSpaceId: 'lot-1',
          name: 'Basic wash',
          price: 150,
        })
      );
      expect(mockToastSuccess).toHaveBeenCalledWith('Add-on created');
    });
  });

  it('toasts when add-on name is missing', async () => {
    const user = userEvent.setup();
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });

    renderPage();
    await waitFor(() => expect(screen.getByText('Downtown Garage')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /add-on services/i }));
    await user.click(screen.getByRole('button', { name: /add service/i }));

    expect(mockToastError).toHaveBeenCalledWith('Add-on name is required');
    expect(mockCreateAncillary).not.toHaveBeenCalled();
  });

  it('shows active reservations on a listing card', async () => {
    const future = new Date(Date.now() + 86400000).toISOString();
    const later = new Date(Date.now() + 2 * 86400000).toISOString();
    mockGetMyListings.mockResolvedValue({ success: true, data: [sampleListing] });
    mockGetVendorBookings.mockResolvedValue({
      success: true,
      data: {
        bookings: [
          {
            id: 'bk-1',
            parkingSpaceId: 'lot-1',
            status: 1,
            userName: 'Asha Patel',
            vehicleNumber: 'MH01AB1234',
            startDateTime: future,
            endDateTime: later,
            slotNumber: 3,
          },
        ],
      },
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/active reservations \(1\)/i)).toBeInTheDocument();
    });
    expect(screen.getByText('Asha Patel')).toBeInTheDocument();
    expect(screen.getByText('MH01AB1234')).toBeInTheDocument();
    expect(screen.getByText('Confirmed')).toBeInTheDocument();
  });

  it('subscribes to notification refresh on mount', async () => {
    renderPage();

    await waitFor(() => {
      expect(mockSubscribeToRefresh).toHaveBeenCalledWith(
        'VendorListings',
        expect.arrayContaining(['booking.created', 'booking.cancelled']),
        expect.any(Function)
      );
    });
  });

  it('shows residential driveway badge', async () => {
    mockGetMyListings.mockResolvedValue({
      success: true,
      data: [
        {
          ...sampleListing,
          id: 'lot-res',
          title: 'Home Driveway',
          listingCategory: 1,
          instantBook: true,
          isLprEnabled: false,
          hasEvCharging: false,
        },
      ],
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText('Home Driveway')).toBeInTheDocument();
    });
    expect(screen.getByText(/🏠 Driveway/)).toBeInTheDocument();
    expect(screen.getByText(/Instant book/)).toBeInTheDocument();
  });
});
