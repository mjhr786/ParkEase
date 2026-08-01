import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import CreateCompany from './CreateCompany';

const mockNavigate = vi.fn();
const mockCreateCompany = vi.fn();
const mockApplySession = vi.fn();
const mockSwitchChannel = vi.fn();
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

let authState = {
  isAuthenticated: true,
  channel: 'Corporate',
  isBootstrap: true,
  isolationEnabled: true,
  applySession: (...a) => mockApplySession(...a),
  switchChannel: (...a) => mockSwitchChannel(...a),
};

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => authState,
}));

vi.mock('../../services/corporateService', () => ({
  default: {
    createCompany: (...args) => mockCreateCompany(...args),
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

async function fillForm(user) {
  await user.type(screen.getByLabelText(/company name/i), 'Acme Corp');
  await user.type(screen.getByLabelText(/registration no/i), 'REG-1');
  await user.type(screen.getByLabelText(/contact email/i), 'a@acme.com');
  await user.type(screen.getByLabelText(/contact phone/i), '555');
  await user.type(screen.getByLabelText(/billing address/i), '1 Main');
}

describe('CreateCompany', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = {
      isAuthenticated: true,
      channel: 'Corporate',
      isBootstrap: true,
      isolationEnabled: true,
      applySession: (...a) => mockApplySession(...a),
      switchChannel: (...a) => mockSwitchChannel(...a),
    };
  });

  afterEach(() => {
    cleanup();
  });

  it('applies session from create response and navigates to dashboard', async () => {
    const user = userEvent.setup();
    mockCreateCompany.mockResolvedValue({
      success: true,
      data: {
        company: { id: 'new-co', name: 'Acme' },
        session: {
          accessToken: 'at',
          refreshToken: 'rt',
          channel: 'Corporate',
          companyId: 'new-co',
          companyRole: 'Admin',
          isBootstrap: false,
        },
      },
    });

    render(
      <MemoryRouter>
        <CreateCompany />
      </MemoryRouter>
    );

    await fillForm(user);
    await user.click(screen.getByRole('button', { name: /create company/i }));

    await waitFor(() => {
      expect(mockApplySession).toHaveBeenCalledWith(
        expect.objectContaining({ accessToken: 'at', companyId: 'new-co' })
      );
      expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard');
    });
    expect(mockSwitchChannel).not.toHaveBeenCalled();
  });

  it('falls back to switchChannel when session is null', async () => {
    const user = userEvent.setup();
    mockCreateCompany.mockResolvedValue({
      success: true,
      data: {
        company: { id: 'new-co2', name: 'Acme' },
        session: null,
      },
    });
    mockSwitchChannel.mockResolvedValue({ success: true });

    render(
      <MemoryRouter>
        <CreateCompany />
      </MemoryRouter>
    );

    await fillForm(user);
    await user.click(screen.getByRole('button', { name: /create company/i }));

    await waitFor(() => {
      expect(mockSwitchChannel).toHaveBeenCalledWith({
        channel: 'Corporate',
        companyId: 'new-co2',
      });
      expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard');
    });
  });

  it('blocks navigation and offers retry when handoff fails', async () => {
    const user = userEvent.setup();
    mockCreateCompany.mockResolvedValue({
      success: true,
      data: {
        company: { id: 'stuck-co' },
        session: null,
      },
    });
    mockSwitchChannel.mockResolvedValue({
      success: false,
      message: 'channel rebind failed',
    });

    render(
      <MemoryRouter>
        <CreateCompany />
      </MemoryRouter>
    );

    await fillForm(user);
    await user.click(screen.getByRole('button', { name: /create company/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /complete setup/i })).toBeInTheDocument();
    });
    expect(mockNavigate).not.toHaveBeenCalledWith('/corporate/dashboard');
  });
});
