import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import Profile from './Profile';

const mockGetCurrentUser = vi.fn();
const mockSetPassword = vi.fn();
const mockLinkExternal = vi.fn();
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: {
      email: 'social@example.com',
      firstName: 'Soc',
      lastName: 'User',
      role: 1,
      createdAt: '2026-01-01T00:00:00Z',
    },
    logout: vi.fn(),
    updateUser: vi.fn(),
    setPassword: (...a) => mockSetPassword(...a),
    linkExternal: (...a) => mockLinkExternal(...a),
  }),
}));

vi.mock('../services/api', () => ({
  default: {
    getCurrentUser: (...a) => mockGetCurrentUser(...a),
    updateProfile: vi.fn(),
    changePassword: vi.fn(),
    deleteProfile: vi.fn(),
    getExternalProviders: vi.fn().mockResolvedValue({
      success: true,
      data: { providers: ['Google'] },
    }),
    getLinkedExternalLogins: vi.fn().mockResolvedValue({
      success: true,
      data: { linkedProviders: ['Google'] },
    }),
  },
}));

vi.mock('../utils/toast.jsx', () => ({
  default: {
    success: (...a) => mockToastSuccess(...a),
    error: (...a) => mockToastError(...a),
  },
}));

function renderProfile() {
  return render(
    <MemoryRouter>
      <Profile />
    </MemoryRouter>
  );
}

describe('Profile page social security', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('shows set-password form when hasPassword is false', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      data: {
        firstName: 'Soc',
        lastName: 'User',
        phoneNumber: '',
        hasPassword: false,
        linkedProviders: ['Google'],
      },
    });

    renderProfile();

    await waitFor(() => {
      expect(screen.getByTestId('set-password-banner')).toBeInTheDocument();
      expect(screen.getByTestId('set-password-form')).toBeInTheDocument();
      expect(screen.getByTestId('linked-Google')).toBeInTheDocument();
    });
    expect(screen.queryByLabelText(/current password/i)).not.toBeInTheDocument();
  });

  it('shows change-password form when hasPassword is true', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      data: {
        firstName: 'Pass',
        lastName: 'User',
        phoneNumber: '',
        hasPassword: true,
        linkedProviders: [],
      },
    });

    renderProfile();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /change password/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /^change password$/i })).toBeInTheDocument();
      expect(document.querySelector('input[name="currentPassword"]')).toBeTruthy();
    });
    expect(screen.queryByTestId('set-password-form')).not.toBeInTheDocument();
    expect(screen.queryByTestId('set-password-banner')).not.toBeInTheDocument();
  });

  it('submits set-password with policy-valid password', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      data: {
        firstName: 'Soc',
        lastName: 'User',
        phoneNumber: '',
        hasPassword: false,
        linkedProviders: ['Google'],
      },
    });
    mockSetPassword.mockResolvedValue({ success: true });

    renderProfile();
    await waitFor(() => {
      expect(screen.getByTestId('set-password-form')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    await user.type(screen.getByTestId('set-password-new'), 'TestPass1!');
    await user.type(screen.getByTestId('set-password-confirm'), 'TestPass1!');
    await user.click(screen.getByRole('button', { name: /set password/i }));

    await waitFor(() => {
      expect(mockSetPassword).toHaveBeenCalledWith('TestPass1!');
      expect(mockToastSuccess).toHaveBeenCalled();
    });
  });
});
