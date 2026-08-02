import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import CompanySwitcher from './CompanySwitcher';

const mockSwitchCompany = vi.fn();
const mockNavigate = vi.fn();
const mockGetMyCompanies = vi.fn();
const mockCreateCompany = vi.fn();
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();
const mockSwitchChannel = vi.fn();
const mockApplySession = vi.fn();

vi.mock('../contexts/CompanyContext', () => ({
  useCompany: () => mockUseCompany(),
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../services/api', () => ({
  default: {},
}));

vi.mock('../services/corporateService', () => ({
  default: {
    getMyCompanies: (...args) => mockGetMyCompanies(...args),
    createCompany: (...args) => mockCreateCompany(...args),
  },
}));

vi.mock('../utils/toast.jsx', () => ({
  default: {
    success: (...args) => mockToastSuccess(...args),
    error: (...args) => mockToastError(...args),
  },
}));

let companyState;
let authState;

function mockUseCompany() {
  return companyState;
}

function mockUseAuth() {
  return authState;
}

function renderSwitcher() {
  return render(
    <MemoryRouter>
      <CompanySwitcher />
    </MemoryRouter>
  );
}

describe('CompanySwitcher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = {
      isAuthenticated: true,
      applySession: mockApplySession,
      channel: 'Marketplace',
      switchChannel: mockSwitchChannel,
      companyId: null,
    };
    companyState = {
      activeCompanyId: null,
      companyDetails: null,
      isCorporateMode: false,
      switchCompany: mockSwitchCompany,
    };
    mockGetMyCompanies.mockResolvedValue({ success: true, data: [] });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders nothing when not authenticated', () => {
    authState = { ...authState, isAuthenticated: false };
    const { container } = renderSwitcher();
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing on Marketplace channel (no cross-product link)', () => {
    const { container } = renderSwitcher();
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole('link', { name: /corporate workspace/i })).not.toBeInTheDocument();
  });

  it('shows company dropdown on Corporate channel without marketplace exit', async () => {
    const user = userEvent.setup();
    authState = {
      ...authState,
      channel: 'Corporate',
      companyId: 'c1',
    };
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    mockGetMyCompanies.mockResolvedValue({
      success: true,
      data: [{ id: 'c1', name: 'Acme Corp' }],
    });

    renderSwitcher();
    await user.click(screen.getByRole('button', { name: /acme corp/i }));

    expect(screen.queryByRole('button', { name: /^personal mode$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /marketplace account/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create corporate account/i })).toBeInTheDocument();
  });

  it('shows company name in corporate mode', () => {
    authState = { ...authState, channel: 'Corporate', companyId: 'c1' };
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    renderSwitcher();
    expect(screen.getByRole('button', { name: /acme corp/i })).toBeInTheDocument();
  });

  it('corporate company switch uses switchChannel', async () => {
    const user = userEvent.setup();
    authState = {
      ...authState,
      channel: 'Corporate',
      companyId: 'c1',
    };
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    mockGetMyCompanies.mockResolvedValue({
      success: true,
      data: [
        { id: 'c1', name: 'Acme Corp' },
        { id: 'c2', name: 'Beta Inc' },
      ],
    });
    mockSwitchChannel.mockResolvedValue({ success: true, channel: 'Corporate', companyId: 'c2' });

    renderSwitcher();
    await user.click(screen.getByRole('button', { name: /acme corp/i }));
    await waitFor(() => expect(screen.getByText('Beta Inc')).toBeInTheDocument());
    await user.click(screen.getByText('Beta Inc'));

    await waitFor(() => {
      expect(mockSwitchChannel).toHaveBeenCalledWith({
        channel: 'Corporate',
        companyId: 'c2',
      });
      expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard', { replace: true });
    });
  });

  it('shows empty companies message when none returned', async () => {
    const user = userEvent.setup();
    authState = { ...authState, channel: 'Corporate', companyId: 'c1' };
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    mockGetMyCompanies.mockResolvedValue({ success: true, data: [] });

    renderSwitcher();
    await user.click(screen.getByRole('button', { name: /acme corp/i }));

    await waitFor(() => {
      expect(screen.getByText(/no corporate accounts found/i)).toBeInTheDocument();
    });
  });

  async function fillCreateForm(user) {
    const textInputs = document.querySelectorAll(
      'form input.form-input[type="text"], form input.form-input[type="email"]'
    );
    await user.type(textInputs[0], 'NewCo');
    await user.type(textInputs[1], 'REG-9');
    await user.type(textInputs[2], 'ops@new.co');
    await user.type(textInputs[3], '555');
    await user.type(document.querySelector('form textarea.form-input'), '1 Main St');
  }

  it('opens create modal and creates company on success', async () => {
    const user = userEvent.setup();
    authState = { ...authState, channel: 'Corporate', companyId: 'c1' };
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    mockCreateCompany.mockResolvedValue({
      success: true,
      data: { company: { id: 'new-c' }, session: null },
    });
    mockSwitchChannel.mockResolvedValue({ success: true });
    mockGetMyCompanies.mockResolvedValue({ success: true, data: [] });

    renderSwitcher();
    await user.click(screen.getByRole('button', { name: /acme corp/i }));
    await user.click(screen.getByRole('button', { name: /create corporate account/i }));

    expect(screen.getByRole('heading', { name: /create corporate account/i })).toBeInTheDocument();

    await fillCreateForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(mockCreateCompany).toHaveBeenCalled();
      expect(mockToastSuccess).toHaveBeenCalled();
      expect(mockSwitchChannel).toHaveBeenCalledWith({
        channel: 'Corporate',
        companyId: 'new-c',
      });
      expect(mockSwitchCompany).toHaveBeenCalledWith('new-c');
      expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard', { replace: true });
    });
  });

  it('toasts error when create company fails', async () => {
    const user = userEvent.setup();
    authState = { ...authState, channel: 'Corporate', companyId: 'c1' };
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    mockCreateCompany.mockResolvedValue({
      success: false,
      message: 'Name taken',
    });

    renderSwitcher();
    await user.click(screen.getByRole('button', { name: /acme corp/i }));
    await user.click(screen.getByRole('button', { name: /create corporate account/i }));

    await fillCreateForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Name taken');
    });
  });
});
