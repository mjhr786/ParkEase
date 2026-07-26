import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import OutboxAdmin from './OutboxAdmin';

const mockNavigate = vi.fn();
const mockGetOutboxMessages = vi.fn();
const mockGetOutboxMessage = vi.fn();
const mockRequeueOutboxMessage = vi.fn();
const mockRequeueAllFailedOutbox = vi.fn();
const mockProcessOutboxNow = vi.fn();
const mockToastError = vi.fn();
const mockToastSuccess = vi.fn();

let authState = { isAdmin: true, loading: false };

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
    getOutboxMessages: (...args) => mockGetOutboxMessages(...args),
    getOutboxMessage: (...args) => mockGetOutboxMessage(...args),
    requeueOutboxMessage: (...args) => mockRequeueOutboxMessage(...args),
    requeueAllFailedOutbox: (...args) => mockRequeueAllFailedOutbox(...args),
    processOutboxNow: (...args) => mockProcessOutboxNow(...args),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: {
    error: (...args) => mockToastError(...args),
    success: (...args) => mockToastSuccess(...args),
  },
}));

function renderOutbox() {
  return render(
    <MemoryRouter>
      <OutboxAdmin />
    </MemoryRouter>
  );
}

const sampleList = {
  summary: { pending: 1, processing: 0, processed: 10, failed: 2, total: 13 },
  items: [
    {
      id: 'msg-1',
      shortTypeName: 'BookingConfirmed',
      idempotencyKey: 'key-1',
      status: 3,
      attemptCount: 3,
      createdAtUtc: '2026-07-26T10:00:00Z',
      lastError: 'SMTP timeout',
    },
  ],
  totalPages: 1,
};

describe('OutboxAdmin', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState = { isAdmin: true, loading: false };
    mockGetOutboxMessages.mockResolvedValue({ success: true, data: sampleList });
  });

  afterEach(() => {
    cleanup();
  });

  it('redirects non-admin users', async () => {
    authState = { isAdmin: false, loading: false };
    renderOutbox();
    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Admin access required');
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
    });
  });

  it('shows spinner while auth is loading', () => {
    authState = { isAdmin: false, loading: true };
    renderOutbox();
    expect(document.querySelector('.spinner')).toBeTruthy();
    expect(mockGetOutboxMessages).not.toHaveBeenCalled();
  });

  it('loads failed messages by default and shows summary', async () => {
    renderOutbox();

    await waitFor(() => {
      expect(mockGetOutboxMessages).toHaveBeenCalledWith(
        expect.objectContaining({ status: 3, page: 1, pageSize: 25 })
      );
    });

    expect(screen.getByRole('heading', { name: /outbox admin/i })).toBeInTheDocument();
    expect(screen.getByText('BookingConfirmed')).toBeInTheDocument();
    expect(screen.getByText('SMTP timeout')).toBeInTheDocument();
    // Summary cards show counts from sample data
    expect(screen.getByText('13')).toBeInTheDocument();
    expect(screen.getByText('key-1')).toBeInTheDocument();
  });

  it('shows empty filter message', async () => {
    mockGetOutboxMessages.mockResolvedValue({
      success: true,
      data: {
        summary: { pending: 0, processing: 0, processed: 0, failed: 0, total: 0 },
        items: [],
        totalPages: 0,
      },
    });

    renderOutbox();

    await waitFor(() => {
      expect(screen.getByText(/no outbox messages match this filter/i)).toBeInTheDocument();
    });
  });

  it('requeues a single message', async () => {
    const user = userEvent.setup();
    mockRequeueOutboxMessage.mockResolvedValue({ success: true });

    renderOutbox();
    await waitFor(() => expect(screen.getByText('BookingConfirmed')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /^requeue$/i }));

    await waitFor(() => {
      expect(mockRequeueOutboxMessage).toHaveBeenCalledWith('msg-1');
      expect(mockToastSuccess).toHaveBeenCalledWith('Message requeued');
    });
  });

  it('requeues all failed after confirm', async () => {
    const user = userEvent.setup();
    mockRequeueAllFailedOutbox.mockResolvedValue({
      success: true,
      message: 'Requeued 2 message(s)',
      data: 2,
    });
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderOutbox();
    await waitFor(() => expect(screen.getByText('BookingConfirmed')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /requeue all failed/i }));

    await waitFor(() => {
      expect(mockRequeueAllFailedOutbox).toHaveBeenCalled();
      expect(mockToastSuccess).toHaveBeenCalledWith('Requeued 2 message(s)');
    });
  });

  it('does not requeue all when confirm declined', async () => {
    const user = userEvent.setup();
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderOutbox();
    await waitFor(() => expect(screen.getByText('BookingConfirmed')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /requeue all failed/i }));
    expect(mockRequeueAllFailedOutbox).not.toHaveBeenCalled();
  });

  it('processes outbox now', async () => {
    const user = userEvent.setup();
    mockProcessOutboxNow.mockResolvedValue({
      success: true,
      data: { message: 'Processed 5', processedCount: 5 },
    });

    renderOutbox();
    await waitFor(() => expect(screen.getByText('BookingConfirmed')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /process now/i }));

    await waitFor(() => {
      expect(mockProcessOutboxNow).toHaveBeenCalledWith(50);
      expect(mockToastSuccess).toHaveBeenCalledWith('Processed 5');
    });
  });

  it('opens detail dialog', async () => {
    const user = userEvent.setup();
    mockGetOutboxMessage.mockResolvedValue({
      success: true,
      data: {
        id: 'msg-1',
        shortTypeName: 'BookingConfirmed',
        status: 3,
        idempotencyKey: 'key-1',
        attemptCount: 3,
        lastError: 'SMTP timeout',
        payloadPreview: '{"bookingId":"b-1"}',
      },
    });

    renderOutbox();
    await waitFor(() => expect(screen.getByText('BookingConfirmed')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /detail/i }));

    await waitFor(() => {
      expect(mockGetOutboxMessage).toHaveBeenCalledWith('msg-1');
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
    expect(screen.getByText('msg-1')).toBeInTheDocument();
    expect(screen.getByText(/bookingId/i)).toBeInTheDocument();
  });

  it('toasts on load failure', async () => {
    mockGetOutboxMessages.mockRejectedValue(new Error('network'));

    renderOutbox();

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });
  });
});
