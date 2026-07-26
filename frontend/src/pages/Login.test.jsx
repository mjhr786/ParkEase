import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import Login from './Login';

const mockLogin = vi.fn();
const mockNavigate = vi.fn();
const mockToastError = vi.fn();

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ login: mockLogin }),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../utils/toast.jsx', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: vi.fn(),
  },
}));

function renderLogin(initialEntry = '/login') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/login" element={<Login />} />
      </Routes>
    </MemoryRouter>
  );
}

function emailInput() {
  return screen.getByPlaceholderText(/enter your email/i);
}

function passwordInput() {
  return screen.getByPlaceholderText(/enter your password/i);
}

describe('Login page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it('renders sign-in form', () => {
    renderLogin();
    expect(screen.getByRole('heading', { name: /welcome back/i })).toBeInTheDocument();
    expect(emailInput()).toBeInTheDocument();
    expect(passwordInput()).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign up/i })).toHaveAttribute('href', '/register');
  });

  it('shows invite copy when returnUrl is invite path', () => {
    renderLogin('/login?returnUrl=%2Finvite%2Faccept%2Ftok');
    expect(
      screen.getByText(/sign in to accept your company invitation/i)
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign up/i })).toHaveAttribute(
      'href',
      '/register?returnUrl=%2Finvite%2Faccept%2Ftok'
    );
  });

  it('navigates to dashboard on successful login', async () => {
    const user = userEvent.setup();
    mockLogin.mockResolvedValue({ success: true });

    renderLogin();
    await user.type(emailInput(), 'user@test.com');
    await user.type(passwordInput(), 'secret');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('user@test.com', 'secret');
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
    });
  });

  it('navigates to safe returnUrl after success', async () => {
    const user = userEvent.setup();
    mockLogin.mockResolvedValue({ success: true });

    renderLogin('/login?returnUrl=%2Fcorporate%2Fdashboard');
    await user.type(emailInput(), 'user@test.com');
    await user.type(passwordInput(), 'secret');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/corporate/dashboard');
    });
  });

  it('ignores open-redirect returnUrl and uses dashboard', async () => {
    const user = userEvent.setup();
    mockLogin.mockResolvedValue({ success: true });

    renderLogin('/login?returnUrl=https%3A%2F%2Fevil.com');
    await user.type(emailInput(), 'user@test.com');
    await user.type(passwordInput(), 'secret');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
    });
  });

  it('toasts on login failure', async () => {
    const user = userEvent.setup();
    mockLogin.mockResolvedValue({ success: false, message: 'Bad password' });

    renderLogin();
    await user.type(emailInput(), 'user@test.com');
    await user.type(passwordInput(), 'wrong');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Bad password');
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });
});
