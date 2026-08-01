import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import AcceptInvitation from './AcceptInvitation';

const mockNavigate = vi.fn();
const mockAcceptInvitation = vi.fn();
const mockSwitchCompany = vi.fn();
const mockSwitchChannel = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let authState = {
  isAuthenticated: true,
  switchChannel: (...args) => mockSwitchChannel(...args),
};

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => authState,
}));

vi.mock('../../contexts/CompanyContext', () => ({
  useCompany: () => ({ switchCompany: mockSwitchCompany }),
}));

vi.mock('../../services/corporateService', () => ({
  default: {
    acceptInvitation: (...args) => mockAcceptInvitation(...args),
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

function renderInvite(token = 'inv-tok') {
  return render(
    <MemoryRouter initialEntries={[`/invite/accept/${token}`]}>
      <Routes>
        <Route path="/invite/accept/:token" element={<AcceptInvitation />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('AcceptInvitation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = {
      isAuthenticated: true,
      switchChannel: (...args) => mockSwitchChannel(...args),
    };
    mockSwitchChannel.mockResolvedValue({ success: true });
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it('redirects unauthenticated users to corporate login with returnUrl', async () => {
    authState = {
      isAuthenticated: false,
      switchChannel: mockSwitchChannel,
    };
    renderInvite('abc');

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Please login to accept the invitation.');
      expect(mockNavigate).toHaveBeenCalledWith(
        expect.stringContaining('/corporate/login?returnUrl=')
      );
    });
    expect(mockNavigate.mock.calls[0][0]).toContain(encodeURIComponent('/invite/accept/abc'));
  });

  it('uses switchChannel after accept (no soft-only path)', async () => {
    mockAcceptInvitation.mockResolvedValue({
      success: true,
      data: { companyId: 'co-iso' },
    });
    mockSwitchChannel.mockResolvedValue({ success: true });

    renderInvite('iso-tok');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /welcome aboard/i })).toBeInTheDocument();
    });
    expect(mockSwitchChannel).toHaveBeenCalledWith({
      channel: 'Corporate',
      companyId: 'co-iso',
    });
    expect(mockSwitchCompany).toHaveBeenCalledWith('co-iso');
    expect(mockToastSuccess).toHaveBeenCalled();
  });

  it('when switchChannel fails, redirects to corporate login with companyId', async () => {
    mockAcceptInvitation.mockResolvedValue({
      success: true,
      data: { companyId: 'co-fail' },
    });
    mockSwitchChannel.mockResolvedValue({ success: false, message: 'membership_required' });

    renderInvite('fail-tok');

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith(
        expect.stringContaining('/corporate/login?companyId=co-fail')
      );
    });
  });

  it('shows error when invitation is invalid', async () => {
    mockAcceptInvitation.mockResolvedValue({
      success: false,
      message: 'Token expired',
    });

    renderInvite('bad-tok');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /invitation failed/i })).toBeInTheDocument();
    });
    expect(screen.getByText('Token expired')).toBeInTheDocument();
  });

  it('shows error on network failure', async () => {
    mockAcceptInvitation.mockRejectedValue(new Error('network'));

    renderInvite('net-tok');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /invitation failed/i })).toBeInTheDocument();
    });
  });

  it('return to dashboard button navigates home', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    mockAcceptInvitation.mockResolvedValue({
      success: false,
      message: 'Token expired',
    });

    renderInvite('bad-tok');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /invitation failed/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /return to dashboard/i }));
    expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
  });
});
