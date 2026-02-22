import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useChatContext } from '../contexts/ChatContext';
import api from '../services/api';
import '@chatscope/chat-ui-kit-styles/dist/default/styles.min.css';
import {
    MainContainer,
    ChatContainer,
    MessageList,
    Message,
    MessageInput,
    Sidebar,
    ConversationList,
    Conversation,
    Avatar,
    ConversationHeader,
    TypingIndicator
} from '@chatscope/chat-ui-kit-react';

export default function Chat() {
    const { conversationId } = useParams();
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const { user } = useAuth();
    const {
        isConnected,
        connectionState,
        registerMessageCallback,
        unregisterMessageCallback,
        registerReadCallback,
        unregisterReadCallback,
        resetUnreadForConversation,
        setActiveConversation,
        onlineUsers
    } = useChatContext();

    const [conversations, setConversations] = useState([]);
    const [messages, setMessages] = useState([]);
    const [selectedConversation, setSelectedConversation] = useState(null);
    const [newMessage, setNewMessage] = useState('');
    const [loading, setLoading] = useState(true);
    const [sendingMessage, setSendingMessage] = useState(false);
    const [showMobileList, setShowMobileList] = useState(!conversationId);
    const [composeMessage, setComposeMessage] = useState('');
    const [composeSending, setComposeSending] = useState(false);
    const [composeError, setComposeError] = useState('');
    const messagesEndRef = useRef(null);
    const messagesContainerRef = useRef(null);
    const messageInputRef = useRef(null);
    const activeConversationRef = useRef(conversationId);

    // Keep ref and global context in sync with route param
    useEffect(() => {
        activeConversationRef.current = conversationId;
        setActiveConversation(conversationId);

        return () => {
            setActiveConversation(null);
        };
    }, [conversationId, setActiveConversation]);

    // New conversation from parking page
    const parkingSpaceId = searchParams.get('parkingSpaceId');

    // Register SignalR callbacks with global ChatContext
    const handleMessageReceived = useCallback((message) => {
        const currentConvId = activeConversationRef.current;

        // Only add to message thread if it belongs to the active conversation
        if (message.conversationId === currentConvId) {
            setMessages(prev => {
                if (prev.some(m => m.id === message.id)) return prev;
                return [...prev, message];
            });
        }

        // Update conversation list (preview, timestamp, and unread count)
        setConversations(prev => {
            const exists = prev.some(c => c.id === message.conversationId);
            if (!exists) {
                // New conversation appeared — reload the full list
                api.getConversations().then(result => {
                    if (result.success) {
                        setConversations(result.data.conversations || []);
                    }
                }).catch(() => { });
                return prev;
            }
            return prev.map(c =>
                c.id === message.conversationId
                    ? {
                        ...c,
                        lastMessagePreview: message.content,
                        lastMessageAt: message.createdAt,
                        unreadCount: message.conversationId !== currentConvId
                            ? (c.unreadCount || 0) + 1
                            : c.unreadCount
                    }
                    : c
            );
        });
    }, []);

    const handleMessagesRead = useCallback((convId) => {
        setMessages(prev => prev.map(m =>
            m.conversationId === convId ? { ...m, isRead: true } : m
        ));
    }, []);

    // Register/unregister callbacks with global context
    useEffect(() => {
        registerMessageCallback(handleMessageReceived);
        registerReadCallback(handleMessagesRead);
        return () => {
            unregisterMessageCallback();
            unregisterReadCallback();
        };
    }, [handleMessageReceived, handleMessagesRead]);

    // Load conversations
    useEffect(() => {
        loadConversations();
    }, []);

    // Load messages when conversation changes
    useEffect(() => {
        if (conversationId) {
            loadMessages(conversationId);
            setShowMobileList(false);

            // Mark as read and clear unread badge locally + globally
            api.markAsRead(conversationId).then(() => {
                setConversations(prev => prev.map(c =>
                    c.id === conversationId ? { ...c, unreadCount: 0 } : c
                ));
                resetUnreadForConversation(conversationId);
            }).catch(() => { });
        }
    }, [conversationId]);

    // Check if we came from a parking page and already have a conversation for it
    useEffect(() => {
        if (!conversationId && parkingSpaceId && conversations.length > 0) {
            const existingConversation = conversations.find(
                c => c.parkingSpaceId === parkingSpaceId
            );
            if (existingConversation) {
                navigate(`/chat/${existingConversation.id}`, { replace: true });
            }
        }
    }, [conversations, parkingSpaceId, conversationId, navigate]);

    // Auto-scroll to bottom on new messages
    useEffect(() => {
        if (messagesEndRef.current) {
            messagesEndRef.current.scrollIntoView({ behavior: 'smooth' });
        }
    }, [messages]);

    const loadConversations = async () => {
        try {
            setLoading(true);
            const result = await api.getConversations();
            if (result.success) {
                setConversations(result.data.conversations || []);
            }
        } catch (err) {
            console.error('Failed to load conversations:', err);
        } finally {
            setLoading(false);
        }
    };

    const loadMessages = async (convId) => {
        try {
            const result = await api.getMessages(convId);
            if (result.success) {
                // Reverse to show oldest first
                setMessages((result.data || []).reverse());
                setSelectedConversation(conversations.find(c => c.id === convId) || null);
            }
        } catch (err) {
            console.error('Failed to load messages:', err);
        }
    };

    const handleComposeAndSend = async (e) => {
        e.preventDefault();
        if (!composeMessage.trim() || composeSending || !parkingSpaceId) return;

        setComposeSending(true);
        setComposeError('');
        try {
            const result = await api.sendMessage({
                parkingSpaceId: parkingSpaceId,
                content: composeMessage.trim()
            });
            if (result.success && result.data) {
                await loadConversations();
                navigate(`/chat/${result.data.conversationId}`, { replace: true });
            } else {
                setComposeError(result?.message || 'Failed to send message');
            }
        } catch (err) {
            const errMsg = err?.response?.data?.message || err?.message || 'Failed to send message. Please try again.';
            setComposeError(errMsg);
            console.error('Failed to start conversation:', err);
        } finally {
            setComposeSending(false);
        }
    };
    const handleSendMessage = async (e) => {
        e.preventDefault();
        if (!newMessage.trim() || sendingMessage) return;

        const conv = conversations.find(c => c.id === conversationId);
        if (!conv) return;

        setSendingMessage(true);
        try {
            const result = await api.sendMessage({
                parkingSpaceId: conv.parkingSpaceId,
                content: newMessage.trim()
            });
            if (result.success && result.data) {
                setMessages(prev => {
                    if (prev.some(m => m.id === result.data.id)) return prev;
                    return [...prev, result.data];
                });
                setNewMessage('');
                setConversations(prev => prev.map(c =>
                    c.id === conversationId
                        ? { ...c, lastMessagePreview: result.data.content, lastMessageAt: result.data.createdAt }
                        : c
                ));
            }
        } catch (err) {
            console.error('Failed to send message:', err);
        } finally {
            setSendingMessage(false);
        }
    };

    // Automatically restore focus when the input re-enables after sending a message
    useEffect(() => {
        if (!sendingMessage && conversationId) {
            // Use a short timeout to ensure Chatscope's internal contenteditable div is fully mounted
            setTimeout(() => {
                const editor = document.querySelector('.cs-message-input__content-editor');
                if (editor) {
                    editor.focus();
                } else {
                    messageInputRef.current?.focus();
                }
            }, 50);
        }
    }, [sendingMessage, conversationId]);

    const selectConversation = (convId) => {
        navigate(`/chat/${convId}`);
        setShowMobileList(false);
    };

    // Format time for chatscope message model
    const formatMessageTime = (dateStr) => {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    };

    // Helper to generate a placeholder avatar based on the user's name
    const getAvatarUrl = (name) => {
        const cleanName = name || 'User';
        return `https://ui-avatars.com/api/?name=${encodeURIComponent(cleanName)}&background=random&color=fff&size=100`;
    };

    return (
        <div className="container" style={{ padding: '1rem 0', maxWidth: '1200px', margin: '0 auto' }}>
            <div style={{
                height: 'calc(100vh - 140px)',
                position: 'relative',
                borderRadius: '16px',
                overflow: 'hidden',
                border: '1px solid var(--color-border)',
                boxShadow: '0 4px 20px rgba(0,0,0,0.08)'
            }}>
                <MainContainer>
                    <Sidebar position="left" scrollable={false} style={{
                        width: '340px',
                        display: showMobileList || window.innerWidth > 768 ? 'flex' : 'none'
                    }}>
                        <div style={{
                            padding: '1.25rem',
                            borderBottom: '1px solid var(--color-border)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            gap: '0.75rem',
                            background: 'var(--color-surface)'
                        }}>
                            <h2 style={{ margin: 0, fontSize: '1.25rem', fontWeight: '700' }}>💬 Messages</h2>
                            <div style={{ display: 'flex', alignItems: 'center' }} title={isConnected ? 'Connected' : 'Reconnecting...'}>
                                <span style={{
                                    display: 'inline-block',
                                    width: '10px', height: '10px', borderRadius: '50%',
                                    background: isConnected ? 'var(--color-success)' : 'var(--color-warning)',
                                    boxShadow: isConnected ? '0 0 8px var(--color-success)' : 'none'
                                }}></span>
                            </div>
                        </div>
                        {loading ? (
                            <div style={{ display: 'flex', justifyContent: 'center', padding: '2rem' }}>
                                <div className="spinner"></div>
                            </div>
                        ) : conversations.length === 0 ? (
                            <div style={{ padding: '2rem', textAlign: 'center', color: 'var(--color-text-muted)' }}>
                                <p>No conversations yet.</p>
                                <button
                                    className="btn btn-primary"
                                    onClick={() => navigate('/search')}
                                    style={{ marginTop: '1rem' }}
                                >
                                    Browse Parking
                                </button>
                            </div>
                        ) : (
                            <ConversationList>
                                {conversations.map(conv => {
                                    const isOnline = onlineUsers?.has(conv.otherParticipantId);
                                    return (
                                        <Conversation
                                            key={conv.id}
                                            name={conv.otherParticipantName}
                                            info={conv.lastMessagePreview || 'No messages yet'}
                                            lastSenderName={conv.parkingSpaceTitle}
                                            unreadCnt={conv.unreadCount > 0 ? conv.unreadCount : undefined}
                                            active={conversationId === conv.id}
                                            onClick={() => selectConversation(conv.id)}
                                            style={{ cursor: 'pointer' }}
                                        >
                                            <Avatar
                                                name={conv.otherParticipantName}
                                                src={getAvatarUrl(conv.otherParticipantName)}
                                                status={isOnline ? 'available' : undefined}
                                            />
                                        </Conversation>
                                    );
                                })}
                            </ConversationList>
                        )}
                    </Sidebar>

                    {conversationId ? (
                        <ChatContainer style={{
                            display: !showMobileList || window.innerWidth > 768 ? 'flex' : 'none'
                        }}>
                            <ConversationHeader>
                                {window.innerWidth <= 768 && (
                                    <ConversationHeader.Back onClick={() => { setShowMobileList(true); navigate('/chat'); }} />
                                )}
                                <Avatar
                                    name={selectedConversation?.otherParticipantName || conversations.find(c => c.id === conversationId)?.otherParticipantName || '?'}
                                    src={getAvatarUrl(selectedConversation?.otherParticipantName || conversations.find(c => c.id === conversationId)?.otherParticipantName || '?')}
                                    status={onlineUsers?.has(selectedConversation?.otherParticipantId || conversations.find(c => c.id === conversationId)?.otherParticipantId) ? 'available' : undefined}
                                />
                                <ConversationHeader.Content
                                    userName={selectedConversation?.otherParticipantName || conversations.find(c => c.id === conversationId)?.otherParticipantName || 'Loading...'}
                                    info={`🅿️ ${selectedConversation?.parkingSpaceTitle || conversations.find(c => c.id === conversationId)?.parkingSpaceTitle || ''}`}
                                />
                            </ConversationHeader>

                            <MessageList
                                typingIndicator={sendingMessage ? <TypingIndicator content="Sending..." /> : null}
                            >
                                {messages.length === 0 ? (
                                    <MessageList.Content
                                        style={{
                                            display: 'flex',
                                            flexDirection: 'column',
                                            justifyContent: 'center',
                                            alignItems: 'center',
                                            height: '100%',
                                            color: 'var(--color-text-muted)'
                                        }}
                                    >
                                        <div style={{ fontSize: '2rem', marginBottom: '0.5rem' }}>👋</div>
                                        <p>Start the conversation!</p>
                                    </MessageList.Content>
                                ) : (
                                    messages.map((msg) => {
                                        const isMine = msg.senderId === user?.id;
                                        return (
                                            <Message
                                                key={msg.id}
                                                model={{
                                                    message: msg.content,
                                                    sender: msg.senderName,
                                                    direction: isMine ? 'outgoing' : 'incoming',
                                                    position: 'single'
                                                }}
                                            >
                                                {!isMine && (
                                                    <Avatar
                                                        name={msg.senderName}
                                                        src={getAvatarUrl(msg.senderName)}
                                                        status={onlineUsers?.has(msg.senderId) ? 'available' : undefined}
                                                    />
                                                )}
                                                {isMine && (
                                                    <Message.Footer>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                            <span>{formatMessageTime(msg.createdAt)}</span>
                                                            <span style={{
                                                                color: msg.isRead ? '#3b82f6' : 'inherit',
                                                                fontSize: '0.9rem',
                                                                marginLeft: '2px'
                                                            }}>
                                                                {msg.isRead ? '✓✓' : '✓'}
                                                            </span>
                                                        </div>
                                                    </Message.Footer>
                                                )}
                                            </Message>
                                        );
                                    })
                                )}
                            </MessageList>

                            <MessageInput
                                placeholder="Type a message..."
                                value={newMessage}
                                onChange={val => setNewMessage(val)}
                                onSend={() => {
                                    if (newMessage.trim()) {
                                        handleSendMessage({ preventDefault: () => { } });
                                    }
                                }}
                                disabled={sendingMessage}
                                attachButton={false}
                                autoFocus
                                ref={messageInputRef}
                            />
                        </ChatContainer>
                    ) : parkingSpaceId && !conversationId ? (
                        /* Compose panel for new conversation */
                        <div style={{
                            flex: 1,
                            display: !showMobileList || window.innerWidth > 768 ? 'flex' : 'none',
                            flexDirection: 'column',
                            justifyContent: 'center', alignItems: 'center',
                            padding: '2rem', background: 'var(--color-surface)'
                        }}>
                            <div style={{ width: '100%', maxWidth: '500px', textAlign: 'center' }}>
                                <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>✉️</div>
                                <h3 style={{ fontSize: '1.2rem', fontWeight: '600', marginBottom: '0.5rem' }}>
                                    New Conversation
                                </h3>
                                <p style={{ fontSize: '0.9rem', color: 'var(--color-text-muted)', marginBottom: '1.5rem' }}>
                                    Send a message to the parking space owner
                                </p>

                                {composeError && (
                                    <div style={{
                                        padding: '0.75rem 1rem', marginBottom: '1rem', borderRadius: '8px',
                                        background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', fontSize: '0.85rem'
                                    }}>
                                        {composeError}
                                    </div>
                                )}

                                <form onSubmit={handleComposeAndSend}>
                                    <textarea
                                        value={composeMessage}
                                        onChange={e => setComposeMessage(e.target.value)}
                                        placeholder="Hi, I have a question about your parking space..."
                                        maxLength={2000}
                                        rows={4}
                                        style={{
                                            width: '100%', padding: '0.85rem 1rem', borderRadius: '12px',
                                            border: '1px solid var(--color-border)', outline: 'none',
                                            background: 'var(--color-surface-elevated, rgba(255,255,255,0.04))',
                                            color: 'var(--color-text)', fontSize: '0.9rem', resize: 'vertical'
                                        }}
                                        disabled={composeSending}
                                        autoFocus
                                    />
                                    <button
                                        type="submit"
                                        disabled={!composeMessage.trim() || composeSending}
                                        className="btn btn-primary"
                                        style={{ width: '100%', marginTop: '1rem', padding: '0.85rem' }}
                                    >
                                        {composeSending ? 'Sending...' : 'Send Message'}
                                    </button>
                                </form>
                            </div>
                        </div>
                    ) : (
                        /* Default empty right pane */
                        <div style={{
                            flex: 1,
                            display: !showMobileList || window.innerWidth > 768 ? 'flex' : 'none',
                            flexDirection: 'column',
                            justifyContent: 'center', alignItems: 'center',
                            color: 'var(--color-text-muted)'
                        }}>
                            <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>💬</div>
                            <button
                                className="btn btn-primary"
                                onClick={() => navigate('/search')}
                                style={{ marginTop: '1rem' }}
                            >
                                Browse Parking
                            </button>
                        </div>
                    )}
                </MainContainer>
            </div>
        </div>
    );
}
