import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import LprRegistry from './LprRegistry';

const mockNavigate = vi.fn();
const mockGetKeys = vi.fn();
const mockGetRules = vi.fn();
const mockCreateKey = vi.fn();
const mockSetKeyEnabled = vi.fn();
const mockDeleteKey = vi.fn();
const mockCreateRule = vi.fn();
const mockSetRuleEnabled = vi.fn();
const mockDeleteRule = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let authState = { isAuthenticated: true, loading: false };

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
    getLprCameraKeys: (...args) => mockGetKeys(...args),
    getLprPlateRules: (...args) => mockGetRules(...args),
    createLprCameraKey: (...args) => mockCreateKey(...args),
    setLprCameraKeyEnabled: (...args) => mockSetKeyEnabled(...args),
    deleteLprCameraKey: (...args) => mockDeleteKey(...args),
    createLprPlateRule: (...args) => mockCreateRule(...args),
    setLprPlateRuleEnabled: (...args) => mockSetRuleEnabled(...args),
    deleteLprPlateRule: (...args) => mockDeleteRule(...args),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

function renderRegistry(spaceId = 'space-99') {
  return render(
    <MemoryRouter initialEntries={[`/vendor/lpr/${spaceId}`]}>
      <Routes>
        <Route path="/vendor/lpr/:parkingSpaceId" element={<LprRegistry />} />
      </Routes>
    </MemoryRouter>
  );
}

const sampleKey = {
  id: 'key-1',
  name: 'North gate',
  keyId: 'cam-north',
  secretPrefix: 'sk_ab',
  isEnabled: true,
};

const sampleRule = {
  id: 'rule-1',
  licensePlateNormalized: 'MH12AB1234',
  ruleType: 2,
  note: 'VIP block',
  isEnabled: true,
};

describe('LprRegistry', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAuthenticated: true, loading: false };
    mockGetKeys.mockResolvedValue({ success: true, data: [] });
    mockGetRules.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('redirects unauthenticated users', () => {
    authState = { isAuthenticated: false, loading: false };
    renderRegistry();
    expect(mockNavigate).toHaveBeenCalledWith('/login');
  });

  it('loads empty registry and shows facility id', async () => {
    renderRegistry('space-99');

    await waitFor(() => {
      expect(mockGetKeys).toHaveBeenCalledWith('space-99');
      expect(mockGetRules).toHaveBeenCalledWith('space-99');
    });

    expect(screen.getByRole('heading', { name: /lpr facility registry/i })).toBeInTheDocument();
    expect(screen.getByText(/Facility ID: space-99/)).toBeInTheDocument();
    expect(screen.getByText(/no camera keys yet/i)).toBeInTheDocument();
    expect(screen.getByText(/no plate rules/i)).toBeInTheDocument();
  });

  it('creates a camera key and shows secret once', async () => {
    const user = userEvent.setup();
    mockCreateKey.mockResolvedValue({
      success: true,
      message: 'Camera key created',
      data: { secret: 'sk_live_secret_once' },
    });
    mockGetKeys
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValueOnce({ success: true, data: [sampleKey] });

    renderRegistry();
    await waitFor(() => {
      expect(screen.getByText(/no camera keys yet/i)).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText(/name \(e\.g\. north gate/i), 'North gate');
    await user.click(screen.getByRole('button', { name: /create camera key/i }));

    await waitFor(() => {
      expect(mockCreateKey).toHaveBeenCalledWith('space-99', {
        name: 'North gate',
        keyId: null,
      });
      expect(mockToastSuccess).toHaveBeenCalledWith('Camera key created');
    });

    expect(screen.getByText(/save this api secret now/i)).toBeInTheDocument();
    expect(screen.getByText('sk_live_secret_once')).toBeInTheDocument();
  });

  it('toasts when key name is empty', async () => {
    const user = userEvent.setup();
    renderRegistry();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /create camera key/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /create camera key/i }));
    expect(mockToastError).toHaveBeenCalledWith('Name is required');
    expect(mockCreateKey).not.toHaveBeenCalled();
  });

  it('lists keys and toggles enable', async () => {
    const user = userEvent.setup();
    mockGetKeys.mockResolvedValue({ success: true, data: [sampleKey] });
    mockSetKeyEnabled.mockResolvedValue({ success: true, message: 'Updated' });

    renderRegistry();
    await waitFor(() => {
      expect(screen.getByText('North gate')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /^disable$/i }));

    await waitFor(() => {
      expect(mockSetKeyEnabled).toHaveBeenCalledWith('space-99', 'key-1', false);
      expect(mockToastSuccess).toHaveBeenCalledWith('Updated');
    });
  });

  it('deletes key after confirm', async () => {
    const user = userEvent.setup();
    mockGetKeys.mockResolvedValue({ success: true, data: [sampleKey] });
    mockDeleteKey.mockResolvedValue({ success: true });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderRegistry();
    await waitFor(() => {
      expect(screen.getByText('North gate')).toBeInTheDocument();
    });

    const deleteButtons = screen.getAllByRole('button', { name: /^delete$/i });
    await user.click(deleteButtons[0]);

    await waitFor(() => {
      expect(mockDeleteKey).toHaveBeenCalledWith('space-99', 'key-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Deleted');
    });
  });

  it('creates plate deny rule', async () => {
    const user = userEvent.setup();
    mockCreateRule.mockResolvedValue({ success: true, message: 'Rule created' });
    mockGetRules
      .mockResolvedValueOnce({ success: true, data: [] })
      .mockResolvedValueOnce({ success: true, data: [sampleRule] });

    renderRegistry();
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/license plate/i)).toBeInTheDocument();
    });

    await user.type(screen.getByPlaceholderText(/license plate/i), 'MH12AB1234');
    await user.type(screen.getByPlaceholderText(/optional note/i), 'VIP block');
    await user.click(screen.getByRole('button', { name: /add plate rule/i }));

    await waitFor(() => {
      expect(mockCreateRule).toHaveBeenCalledWith('space-99', {
        licensePlate: 'MH12AB1234',
        ruleType: 2,
        note: 'VIP block',
      });
      expect(mockToastSuccess).toHaveBeenCalledWith('Rule created');
    });
  });

  it('toasts when plate is empty on rule create', async () => {
    const user = userEvent.setup();
    renderRegistry();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add plate rule/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /add plate rule/i }));
    expect(mockToastError).toHaveBeenCalledWith('License plate is required');
    expect(mockCreateRule).not.toHaveBeenCalled();
  });

  it('lists rules and deletes after confirm', async () => {
    const user = userEvent.setup();
    mockGetRules.mockResolvedValue({ success: true, data: [sampleRule] });
    mockDeleteRule.mockResolvedValue({ success: true });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderRegistry();
    await waitFor(() => {
      expect(screen.getByText('MH12AB1234')).toBeInTheDocument();
      expect(screen.getByText('VIP block')).toBeInTheDocument();
      // "Deny" also appears in help copy + select option
      expect(screen.getAllByText('Deny').length).toBeGreaterThanOrEqual(1);
    });

    const deleteButtons = screen.getAllByRole('button', { name: /^delete$/i });
    await user.click(deleteButtons[0]);

    await waitFor(() => {
      expect(mockDeleteRule).toHaveBeenCalledWith('space-99', 'rule-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Deleted');
    });
  });

  it('toasts load failures', async () => {
    mockGetKeys.mockResolvedValue({ success: false, message: 'Keys failed' });
    mockGetRules.mockResolvedValue({ success: false, message: 'Rules failed' });

    renderRegistry();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Keys failed');
      expect(mockToastError).toHaveBeenCalledWith('Rules failed');
    });
  });
});
