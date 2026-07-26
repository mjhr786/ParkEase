import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import Search from './Search';

const mockNavigate = vi.fn();
const mockSearchParking = vi.fn();
const mockGetMapParking = vi.fn();
const mockGetMyFavorites = vi.fn();
const mockToggleFavorite = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let authState = { isAuthenticated: false, user: null };

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
    searchParking: (...args) => mockSearchParking(...args),
    getMapParking: (...args) => mockGetMapParking(...args),
    getMyFavorites: (...args) => mockGetMyFavorites(...args),
    toggleFavorite: (...args) => mockToggleFavorite(...args),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
    loading: vi.fn(() => 'toast-id'),
    dismiss: vi.fn(),
  },
}));

vi.mock('../components/LocationMap', () => ({
  default: () => <div data-testid="location-map" />,
}));

vi.mock('../components/BookedSlots', () => ({
  default: () => null,
}));

function renderSearch(initialEntry = '/search') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/search" element={<Search />} />
      </Routes>
    </MemoryRouter>
  );
}

const sampleSpace = {
  id: 'ps-1',
  title: 'Central Garage',
  address: '1 Main St',
  city: 'Mumbai',
  hourlyRate: 50,
  averageRating: 4.5,
  parkingType: 0,
  availableSpots: 3,
  totalSpots: 10,
  is24Hours: true,
  imageUrls: [],
};

describe('Search page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAuthenticated: false, user: null };
    mockSearchParking.mockResolvedValue({
      success: true,
      data: { parkingSpaces: [], totalPages: 1 },
    });
    mockGetMapParking.mockResolvedValue({ success: true, data: [] });
    mockGetMyFavorites.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders Find Parking heading and filter form', async () => {
    renderSearch();
    expect(screen.getByRole('heading', { name: /find parking/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /search/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /near me/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/street or area/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(mockSearchParking).toHaveBeenCalled();
      expect(mockGetMapParking).toHaveBeenCalled();
    });
  });

  it('shows empty state when no spaces returned', async () => {
    renderSearch();
    await waitFor(() => {
      expect(screen.getByText(/no parking spaces found/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/try adjusting your search filters/i)).toBeInTheDocument();
  });

  it('renders parking cards from search results', async () => {
    mockSearchParking.mockResolvedValue({
      success: true,
      data: { parkingSpaces: [sampleSpace], totalPages: 1 },
    });

    renderSearch();

    await waitFor(() => {
      expect(screen.getByText('Central Garage')).toBeInTheDocument();
    });
    expect(screen.getByText(/1 Main St, Mumbai/i)).toBeInTheDocument();
    expect(screen.getByText(/1 parking spot found/i)).toBeInTheDocument();
    expect(screen.getByTestId('location-map')).toBeInTheDocument();
  });

  it('navigates to parking details when card is clicked', async () => {
    const user = userEvent.setup();
    mockSearchParking.mockResolvedValue({
      success: true,
      data: { parkingSpaces: [sampleSpace], totalPages: 1 },
    });

    renderSearch();
    await waitFor(() => expect(screen.getByText('Central Garage')).toBeInTheDocument());

    await user.click(screen.getByText('Central Garage'));
    expect(mockNavigate).toHaveBeenCalledWith('/parking/ps-1');
  });

  it('prompts login when guest favorites a space', async () => {
    const user = userEvent.setup();
    mockSearchParking.mockResolvedValue({
      success: true,
      data: { parkingSpaces: [sampleSpace], totalPages: 1 },
    });

    renderSearch();
    await waitFor(() => expect(screen.getByText('Central Garage')).toBeInTheDocument());

    const favBtn = screen.getByRole('button', { name: '🤍' });
    await user.click(favBtn);

    expect(mockToastError).toHaveBeenCalledWith('Please log in to save favorites');
    expect(mockNavigate).toHaveBeenCalledWith('/login');
    expect(mockToggleFavorite).not.toHaveBeenCalled();
  });

  it('toggles favorite when authenticated', async () => {
    const user = userEvent.setup();
    authState = { isAuthenticated: true, user: { id: 'u1' } };
    mockSearchParking.mockResolvedValue({
      success: true,
      data: { parkingSpaces: [sampleSpace], totalPages: 1 },
    });
    mockToggleFavorite.mockResolvedValue({ success: true, data: true });

    renderSearch();
    await waitFor(() => expect(screen.getByText('Central Garage')).toBeInTheDocument());
    await waitFor(() => expect(mockGetMyFavorites).toHaveBeenCalled());

    await user.click(screen.getByRole('button', { name: '🤍' }));

    await waitFor(() => {
      expect(mockToggleFavorite).toHaveBeenCalledWith('ps-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Added to favorites');
    });
  });

  it('submits search with address filter', async () => {
    const user = userEvent.setup();
    renderSearch();
    await waitFor(() => expect(mockSearchParking).toHaveBeenCalled());

    mockSearchParking.mockClear();
    mockGetMapParking.mockClear();

    await user.type(screen.getByPlaceholderText(/street or area/i), 'Andheri');
    await user.click(screen.getByRole('button', { name: /search/i }));

    await waitFor(() => {
      expect(mockSearchParking).toHaveBeenCalled();
      const params = mockSearchParking.mock.calls.at(-1)[0];
      expect(params.address).toBe('Andheri');
    });
  });

  it('opens Near Me radius dialog', async () => {
    const user = userEvent.setup();
    // Ensure geolocation exists so handleNearMeClick opens dialog
    const geo = {
      getCurrentPosition: vi.fn(),
    };
    Object.defineProperty(global.navigator, 'geolocation', {
      value: geo,
      configurable: true,
    });

    renderSearch();
    await waitFor(() => expect(mockSearchParking).toHaveBeenCalled());

    await user.click(screen.getByRole('button', { name: /near me/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/search near you/i)).toBeInTheDocument();
  });
});
