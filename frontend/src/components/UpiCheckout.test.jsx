import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, fireEvent, waitFor } from '@testing-library/react';
import UpiCheckout from './UpiCheckout';

const mockVerifyPayment = vi.fn();

vi.mock('../services/api', () => ({
  default: {
    verifyPayment: (...args) => mockVerifyPayment(...args),
  },
}));

const session = {
  providerPaymentId: 'pay_abc',
  providerOrderId: 'order_abc',
  upiIntentUrl: 'upi://pay?pa=merchant@upi&pn=ParkEase&am=150.00&cu=INR',
  displayVpa: 'merchant@upi',
  expiresAtUtc: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
};

describe('UpiCheckout', () => {
  let writeText;

  beforeEach(() => {
    vi.clearAllMocks();
    mockVerifyPayment.mockResolvedValue({ success: false });
    writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });
  });

  afterEach(() => {
    cleanup();
  });

  it('copies the full intent URI, never DisplayVpa alone', async () => {
    render(
      <UpiCheckout
        session={session}
        bookingId="b-1"
        onSuccess={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: /copy upi uri/i }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(session.upiIntentUrl));
    expect(writeText).not.toHaveBeenCalledWith(session.displayVpa);
    expect(screen.getByRole('button', { name: /copied full uri/i })).toBeInTheDocument();
  });

  it('renders the full URI in a selectable field', () => {
    render(
      <UpiCheckout
        session={session}
        bookingId="b-1"
        onSuccess={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByLabelText(/full upi intent uri/i)).toHaveValue(session.upiIntentUrl);
  });
});
