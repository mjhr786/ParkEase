import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CompanySwitcher from './CompanySwitcher';

const mockSwitchCompany = vi.fn();
const mockNavigate = vi.fn();
const mockGetMyCompanies = vi.fn();
const mockCreateCompany = vi.fn();
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('../contexts/CompanyContext', () => ({
  useCompany: () => mockUseCompany(),
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

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

describe('CompanySwitcher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAuthenticated: true };
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
    authState = { isAuthenticated: false };
    const { container } = render(<CompanySwitcher />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows Personal Mode by default', () => {
    render(<CompanySwitcher />);
    expect(screen.getByRole('button', { name: /personal mode/i })).toBeInTheDocument();
  });

  it('shows company name in corporate mode', () => {
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };
    render(<CompanySwitcher />);
    expect(screen.getByRole('button', { name: /acme corp/i })).toBeInTheDocument();
  });

  it('opens dropdown, loads companies, and switches to corporate', async () => {
    const user = userEvent.setup();
    mockGetMyCompanies.mockResolvedValue({
      success: true,
      data: [{ id: 'c1', name: 'Acme Corp' }],
    });

    render(<CompanySwitcher />);
    await user.click(screen.getByRole('button', { name: /personal mode/i }));

    await waitFor(() => {
      expect(mockGetMyCompanies).toHaveBeenCalled();
      expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Acme Corp'));
    expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard', { replace: true });
    expect(mockSwitchCompany).toHaveBeenCalledWith('c1');
  });

  it('switches back to personal mode', async () => {
    const user = userEvent.setup();
    companyState = {
      activeCompanyId: 'c1',
      companyDetails: { name: 'Acme Corp' },
      isCorporateMode: true,
      switchCompany: mockSwitchCompany,
    };

    render(<CompanySwitcher />);
    await user.click(screen.getByRole('button', { name: /acme corp/i }));
    await user.click(screen.getByRole('button', { name: /personal mode/i }));

    expect(mockNavigate).toHaveBeenCalledWith('/dashboard', { replace: true });
    expect(mockSwitchCompany).toHaveBeenCalledWith(null);
  });

  it('shows empty companies message when none returned', async () => {
    const user = userEvent.setup();
    mockGetMyCompanies.mockResolvedValue({ success: true, data: [] });

    render(<CompanySwitcher />);
    await user.click(screen.getByRole('button', { name: /personal mode/i }));

    await waitFor(() => {
      expect(screen.getByText(/no corporate accounts found/i)).toBeInTheDocument();
    });
  });

  async function fillCreateForm(user) {
    const textInputs = document.querySelectorAll(
      'form input.form-input[type="text"], form input.form-input[type="email"]'
    );
    // name, registration, email, phone
    await user.type(textInputs[0], 'NewCo');
    await user.type(textInputs[1], 'REG-9');
    await user.type(textInputs[2], 'ops@new.co');
    await user.type(textInputs[3], '555');
    await user.type(document.querySelector('form textarea.form-input'), '1 Main St');
  }

  it('opens create modal and creates company on success', async () => {
    const user = userEvent.setup();
    mockCreateCompany.mockResolvedValue({
      success: true,
      data: { id: 'new-c' },
    });
    mockGetMyCompanies.mockResolvedValue({ success: true, data: [] });

    render(<CompanySwitcher />);
    await user.click(screen.getByRole('button', { name: /personal mode/i }));
    await user.click(screen.getByRole('button', { name: /create corporate account/i }));

    expect(screen.getByRole('heading', { name: /create corporate account/i })).toBeInTheDocument();

    await fillCreateForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(mockCreateCompany).toHaveBeenCalled();
      expect(mockToastSuccess).toHaveBeenCalled();
      expect(mockSwitchCompany).toHaveBeenCalledWith('new-c');
      expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard', { replace: true });
    });
  });

  it('toasts error when create company fails', async () => {
    const user = userEvent.setup();
    mockCreateCompany.mockResolvedValue({
      success: false,
      message: 'Name taken',
    });

    render(<CompanySwitcher />);
    await user.click(screen.getByRole('button', { name: /personal mode/i }));
    await user.click(screen.getByRole('button', { name: /create corporate account/i }));

    await fillCreateForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Name taken');
    });
  });
});
