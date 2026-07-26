import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import Chat from './Chat';

const mockNavigate = vi.fn();
const mockGetConversations = vi.fn();
const mockGetMessages = vi.fn();
const mockMarkAsRead = vi.fn();
const mockSendMessage = vi.fn();
const mockRegisterMessageCallback = vi.fn();
const mockUnregisterMessageCallback = vi.fn();
const mockRegisterReadCallback = vi.fn();
const mockUnregisterReadCallback = vi.fn();
const mockSyncUnreadFromConversations = vi.fn();
const mockSetActiveConversation = vi.fn();

let authUser = { id: 'u-1', fullName: 'Test User' };

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: authUser }),
}));

vi.mock('../contexts/ChatContext', () => ({
  useChatContext: () => ({
    isConnected: true,
    registerMessageCallback: (...args) => mockRegisterMessageCallback(...args),
    unregisterMessageCallback: (...args) => mockUnregisterMessageCallback(...args),
    registerReadCallback: (...args) => mockRegisterReadCallback(...args),
    unregisterReadCallback: (...args) => mockUnregisterReadCallback(...args),
    syncUnreadFromConversations: (...args) => mockSyncUnreadFromConversations(...args),
    setActiveConversation: (...args) => mockSetActiveConversation(...args),
    onlineUsers: new Set(),
  }),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../services/api', () => ({
  default: {
    getConversations: (...args) => mockGetConversations(...args),
    getMessages: (...args) => mockGetMessages(...args),
    markAsRead: (...args) => mockMarkAsRead(...args),
    sendMessage: (...args) => mockSendMessage(...args),
  },
}));

vi.mock('@chatscope/chat-ui-kit-styles/dist/default/styles.min.css', () => ({}));

vi.mock('@chatscope/chat-ui-kit-react', () => {
  // Strip non-DOM props to avoid React unknown-prop warnings in tests
  const passthrough = ({ children }) => <div>{children}</div>;
  return {
    MainContainer: passthrough,
    ChatContainer: passthrough,
    MessageList: Object.assign(
      ({ children }) => <div>{children}</div>,
      { Content: ({ children }) => <div>{children}</div> }
    ),
    Message: Object.assign(
      ({ children, model }) => (
        <div data-testid="chat-message" data-direction={model?.direction}>
          {model?.message}
          {children}
        </div>
      ),
      { Footer: ({ children }) => <div>{children}</div> }
    ),
    MessageInput: ({ value, onChange, onSend, placeholder, disabled }) => (
      <div>
        <input
          aria-label="message-input"
          value={value}
          disabled={disabled}
          placeholder={placeholder}
          onChange={(e) => onChange?.(e.target.value)}
        />
        <button type="button" onClick={() => onSend?.()} disabled={disabled}>
          Send
        </button>
      </div>
    ),
    Sidebar: ({ children }) => <div>{children}</div>,
    ConversationList: ({ children }) => <div>{children}</div>,
    Conversation: ({ name, info, onClick, children }) => (
      <button type="button" onClick={onClick}>
        <span>{name}</span>
        <span>{info}</span>
        {children}
      </button>
    ),
    Avatar: () => null,
    ConversationHeader: Object.assign(
      ({ children }) => <div>{children}</div>,
      {
        Back: ({ onClick }) => (
          <button type="button" onClick={onClick}>
            Back
          </button>
        ),
        Content: ({ userName, info }) => (
          <div>
            <span>{userName}</span>
            <span>{info}</span>
          </div>
        ),
      }
    ),
    TypingIndicator: ({ content }) => <div>{content}</div>,
  };
});

function renderChat(initialEntry = '/chat') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/chat" element={<Chat />} />
        <Route path="/chat/:conversationId" element={<Chat />} />
      </Routes>
    </MemoryRouter>
  );
}

const conversation = {
  id: 'c-1',
  otherParticipantId: 'u-2',
  otherParticipantName: 'Owner Sam',
  parkingSpaceTitle: 'Airport Lot',
  parkingSpaceId: 'ps-1',
  lastMessagePreview: 'Hello there',
  unreadCount: 2,
};

describe('Chat page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authUser = { id: 'u-1', fullName: 'Test User' };
    mockGetConversations.mockResolvedValue({
      success: true,
      data: { conversations: [] },
    });
    mockGetMessages.mockResolvedValue({ success: true, data: [] });
    mockMarkAsRead.mockResolvedValue({ success: true });
  });

  afterEach(() => {
    cleanup();
  });

  it('loads conversations and shows empty state', async () => {
    renderChat();

    await waitFor(() => {
      expect(mockGetConversations).toHaveBeenCalled();
    });
    expect(screen.getByRole('heading', { name: /messages/i })).toBeInTheDocument();
    expect(screen.getByText(/no conversations yet/i)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /browse parking/i }).length).toBeGreaterThan(0);
  });

  it('lists conversations and navigates on select', async () => {
    const user = userEvent.setup();
    mockGetConversations.mockResolvedValue({
      success: true,
      data: { conversations: [conversation] },
    });

    renderChat();

    await waitFor(() => {
      expect(screen.getByText('Owner Sam')).toBeInTheDocument();
    });
    expect(screen.getByText('Hello there')).toBeInTheDocument();
    expect(mockSyncUnreadFromConversations).toHaveBeenCalledWith([conversation]);

    await user.click(screen.getByText('Owner Sam'));
    expect(mockNavigate).toHaveBeenCalledWith('/chat/c-1');
  });

  it('opens thread and loads messages', async () => {
    mockGetConversations.mockResolvedValue({
      success: true,
      data: { conversations: [conversation] },
    });
    mockGetMessages.mockResolvedValue({
      success: true,
      data: [
        {
          id: 'm-1',
          conversationId: 'c-1',
          senderId: 'u-2',
          senderName: 'Owner Sam',
          content: 'Welcome!',
          isRead: true,
          createdAt: '2026-07-26T10:00:00Z',
        },
      ],
    });

    renderChat('/chat/c-1');

    await waitFor(() => {
      expect(mockGetMessages).toHaveBeenCalledWith('c-1');
      expect(mockMarkAsRead).toHaveBeenCalledWith('c-1');
    });
    await waitFor(() => {
      expect(screen.getByText('Welcome!')).toBeInTheDocument();
    });
    expect(mockSetActiveConversation).toHaveBeenCalledWith('c-1');
  });

  it('sends a message in an open thread', async () => {
    const user = userEvent.setup();
    mockGetConversations.mockResolvedValue({
      success: true,
      data: { conversations: [conversation] },
    });
    mockGetMessages.mockResolvedValue({ success: true, data: [] });
    mockSendMessage.mockResolvedValue({
      success: true,
      data: {
        id: 'm-new',
        conversationId: 'c-1',
        senderId: 'u-1',
        senderName: 'Test User',
        content: 'Is the gate open?',
        isRead: false,
        createdAt: '2026-07-26T11:00:00Z',
      },
    });

    renderChat('/chat/c-1');
    await waitFor(() => expect(mockGetMessages).toHaveBeenCalled());

    const input = screen.getByLabelText('message-input');
    await user.type(input, 'Is the gate open?');
    await user.click(screen.getByRole('button', { name: /^send$/i }));

    await waitFor(() => {
      expect(mockSendMessage).toHaveBeenCalledWith({
        parkingSpaceId: 'ps-1',
        content: 'Is the gate open?',
        conversationId: 'c-1',
      });
    });
    await waitFor(() => {
      const bubbles = screen.getAllByTestId('chat-message');
      expect(bubbles.some((el) => el.textContent.includes('Is the gate open?'))).toBe(true);
    });
  });

  it('shows compose panel for new parking conversation', async () => {
    const user = userEvent.setup();
    mockSendMessage.mockResolvedValue({
      success: true,
      data: { conversationId: 'c-new' },
    });
    mockGetConversations
      .mockResolvedValueOnce({ success: true, data: { conversations: [] } })
      .mockResolvedValue({
        success: true,
        data: {
          conversations: [{ ...conversation, id: 'c-new', parkingSpaceId: 'ps-99' }],
        },
      });

    renderChat('/chat?parkingSpaceId=ps-99');

    await waitFor(() => {
      expect(screen.getByText(/new conversation/i)).toBeInTheDocument();
    });

    await user.type(
      screen.getByPlaceholderText(/question about your parking space/i),
      'Hi, is EV available?'
    );
    await user.click(screen.getByRole('button', { name: /send message/i }));

    await waitFor(() => {
      expect(mockSendMessage).toHaveBeenCalledWith({
        parkingSpaceId: 'ps-99',
        content: 'Hi, is EV available?',
      });
      expect(mockNavigate).toHaveBeenCalledWith('/chat/c-new', { replace: true });
    });
  });

  it('registers SignalR message/read callbacks', async () => {
    renderChat();
    await waitFor(() => {
      expect(mockRegisterMessageCallback).toHaveBeenCalledWith(expect.any(Function));
      expect(mockRegisterReadCallback).toHaveBeenCalledWith(expect.any(Function));
    });
  });

  it('navigates to search from empty pane browse button', async () => {
    const user = userEvent.setup();
    renderChat();
    await waitFor(() => expect(mockGetConversations).toHaveBeenCalled());

    const browseButtons = screen.getAllByRole('button', { name: /browse parking/i });
    await user.click(browseButtons[0]);
    expect(mockNavigate).toHaveBeenCalledWith('/search');
  });
});
