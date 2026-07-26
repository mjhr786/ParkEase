import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import StripeCheckout from './StripeCheckout';
import { ThemeProvider } from '../contexts/ThemeContext';

function renderWithTheme(ui) {
  return render(<ThemeProvider>{ui}</ThemeProvider>);
}

const mockConfirmPayment = vi.fn();
const mockSubmit = vi.fn();
const mockLoadStripe = vi.fn(() => Promise.resolve({}));

let stripeReady = true;

vi.mock('@stripe/stripe-js', () => ({
  loadStripe: (...args) => mockLoadStripe(...args),
}));

vi.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children }) => <div data-testid="elements">{children}</div>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: () =>
    stripeReady
      ? {
          confirmPayment: (...args) => mockConfirmPayment(...args),
        }
      : null,
  useElements: () =>
    stripeReady
      ? {
          submit: (...args) => mockSubmit(...args),
        }
      : null,
}));

describe('StripeCheckout', () => {
  const onSuccess = vi.fn();
  const onCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    stripeReady = true;
    mockSubmit.mockResolvedValue({});
    mockConfirmPayment.mockResolvedValue({
      paymentIntent: { id: 'pi_123', status: 'succeeded' },
    });
  });

  afterEach(() => {
    cleanup();
  });

  it('renders nothing without clientSecret or publishableKey', () => {
    const { container: a } = renderWithTheme(
      <StripeCheckout clientSecret={null} publishableKey="pk" onSuccess={onSuccess} onCancel={onCancel} />
    );
    expect(a).toBeEmptyDOMElement();

    const { container: b } = renderWithTheme(
      <StripeCheckout clientSecret="cs" publishableKey={null} onSuccess={onSuccess} onCancel={onCancel} />
    );
    expect(b).toBeEmptyDOMElement();
  });

  it('renders payment form when secrets are present', () => {
    renderWithTheme(
      <StripeCheckout
        clientSecret="cs_test"
        publishableKey="pk_test"
        onSuccess={onSuccess}
        onCancel={onCancel}
      />
    );
    expect(screen.getByTestId('payment-element')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /pay now/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    expect(mockLoadStripe).toHaveBeenCalledWith('pk_test');
  });

  it('calls onCancel when Cancel is clicked', async () => {
    const user = userEvent.setup();
    renderWithTheme(
      <StripeCheckout
        clientSecret="cs_test"
        publishableKey="pk_test"
        onSuccess={onSuccess}
        onCancel={onCancel}
      />
    );
    await user.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('calls onSuccess when payment succeeds', async () => {
    const user = userEvent.setup();
    renderWithTheme(
      <StripeCheckout
        clientSecret="cs_test"
        publishableKey="pk_test"
        onSuccess={onSuccess}
        onCancel={onCancel}
      />
    );

    await user.click(screen.getByRole('button', { name: /pay now/i }));

    await waitFor(() => {
      expect(mockSubmit).toHaveBeenCalled();
      expect(mockConfirmPayment).toHaveBeenCalled();
      expect(onSuccess).toHaveBeenCalledWith('pi_123');
    });
  });

  it('shows submit error message', async () => {
    const user = userEvent.setup();
    mockSubmit.mockResolvedValue({ error: { message: 'Card incomplete' } });

    renderWithTheme(
      <StripeCheckout
        clientSecret="cs_test"
        publishableKey="pk_test"
        onSuccess={onSuccess}
        onCancel={onCancel}
      />
    );

    await user.click(screen.getByRole('button', { name: /pay now/i }));

    await waitFor(() => {
      expect(screen.getByText('Card incomplete')).toBeInTheDocument();
    });
    expect(onSuccess).not.toHaveBeenCalled();
  });

  it('shows confirm error message', async () => {
    const user = userEvent.setup();
    mockConfirmPayment.mockResolvedValue({
      error: { message: 'Card declined' },
    });

    renderWithTheme(
      <StripeCheckout
        clientSecret="cs_test"
        publishableKey="pk_test"
        onSuccess={onSuccess}
        onCancel={onCancel}
      />
    );

    await user.click(screen.getByRole('button', { name: /pay now/i }));

    await waitFor(() => {
      expect(screen.getByText('Card declined')).toBeInTheDocument();
    });
    expect(onSuccess).not.toHaveBeenCalled();
  });

  it('shows incomplete payment message when intent not succeeded', async () => {
    const user = userEvent.setup();
    mockConfirmPayment.mockResolvedValue({
      paymentIntent: { id: 'pi_pending', status: 'requires_action' },
    });

    renderWithTheme(
      <StripeCheckout
        clientSecret="cs_test"
        publishableKey="pk_test"
        onSuccess={onSuccess}
        onCancel={onCancel}
      />
    );

    await user.click(screen.getByRole('button', { name: /pay now/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/payment was not completed/i)
      ).toBeInTheDocument();
    });
  });
});
