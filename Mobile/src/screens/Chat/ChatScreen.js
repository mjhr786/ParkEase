/**
 * Chat Screen
 * Displays message thread for a conversation with real-time updates
 */

import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
    View, Text, FlatList, TextInput, TouchableOpacity,
    StyleSheet, KeyboardAvoidingView, Platform
} from 'react-native';
import { MessagesSkeleton } from '../../components/Common/ShimmerPlaceholder';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../../styles/globalStyles';
import { useAuth } from '../../hooks/useAuth';
import { useDispatch } from 'react-redux';
import { getUnreadCountThunk } from '../../store/slices/chatSlice';
import chatService from '../../services/chat/chatService';
import { EventBus } from '../../utils/EventBus';

const ChatScreen = ({ route, navigation }) => {
    const { conversationId: initialConversationId, parkingSpaceId, participantName, parkingTitle } = route.params;
    const { user } = useAuth();
    const dispatch = useDispatch();
    const insets = useSafeAreaInsets();
    const [activeConversationId, setActiveConversationId] = useState(initialConversationId);
    const [messages, setMessages] = useState([]);
    const [newMessage, setNewMessage] = useState('');
    const [loading, setLoading] = useState(true);
    const [sending, setSending] = useState(false);
    const flatListRef = useRef(null);
    const pollInterval = useRef(null);

    useEffect(() => {
        if (activeConversationId) {
            loadMessages();
            markRead();

            // Poll for new messages every 5 seconds (lightweight real-time substitute for mobile)
            pollInterval.current = setInterval(loadMessages, 5000);
        } else {
            // Ensure minimum shimmer time for smooth transition even on new chat
            setTimeout(() => setLoading(false), 600);
        }
        return () => {
            if (pollInterval.current) clearInterval(pollInterval.current);
        };
    }, [activeConversationId]);

    const loadMessages = async () => {
        if (!activeConversationId) return;
        try {
            const result = await chatService.getMessages(activeConversationId);
            if (result.success) {
                const newMessages = (result.data || []).reverse();
                setMessages(prev => {
                    // Keep optimistic messages that are still sending or have errors
                    const optimistic = prev.filter(m => m.status === 'sending' || m.status === 'error');
                    
                    // Simple deduplication based on ID
                    const realIds = new Set(newMessages.map(m => m.id));
                    const filteredOptimistic = optimistic.filter(m => !realIds.has(m.id));
                    
                    return [...newMessages, ...filteredOptimistic];
                });
            }
        } catch (error) {
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Network Issue', message: 'Failed to load messages' });
        } finally {
            // Ensure minimum shimmer time for smooth transition
            setTimeout(() => setLoading(false), 600);
        }
    };

    const markRead = async () => {
        if (!activeConversationId) return;
        try {
            const result = await chatService.markAsRead(activeConversationId);
            if (result.success) {
                dispatch(getUnreadCountThunk());
            }
        } catch { }
    };

    const handleSend = async () => {
        const content = newMessage.trim();
        if (!content || sending) return;

        const tempId = `temp-${Date.now()}`;
        const tempMessage = {
            id: tempId,
            content,
            senderId: user?.id,
            senderName: `${user?.firstName} ${user?.lastName}`,
            createdAt: new Date().toISOString(),
            status: 'sending',
            isRead: false
        };

        // UI updates immediately for better UX
        setMessages(prev => [...prev, tempMessage]);
        setNewMessage('');
        setSending(true);

        // Auto-scroll
        setTimeout(() => flatListRef.current?.scrollToEnd({ animated: true }), 50);

        try {
            const result = await chatService.sendMessage(parkingSpaceId, content);
            if (result.success && result.data) {
                // Replace optimistic message with real message from server
                setMessages(prev => prev.map(m => m.id === tempId ? result.data : m));

                // If this was a new conversation, set the active conversation ID to start polling
                if (!activeConversationId && result.data.conversationId) {
                    setActiveConversationId(result.data.conversationId);
                }
            } else {
                throw new Error('Failed to send');
            }
        } catch (error) {
            // Update message status to error
            setMessages(prev => prev.map(m => m.id === tempId ? { ...m, status: 'error' } : m));
            EventBus.emit('SHOW_ERROR_BANNER', { title: 'Network Issue', message: 'Failed to send message' });
        } finally {
            setSending(false);
        }
    };

    const formatTime = (dateStr) => {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    };

    const renderMessage = ({ item }) => {
        const isMine = item.senderId === user?.id;
        const isSending = item.status === 'sending';
        const isError = item.status === 'error';

        return (
            <View style={[styles.messageBubbleRow, isMine && styles.messageBubbleRowMine]}>
                <View style={[
                    styles.messageBubble,
                    isMine ? styles.myBubble : styles.otherBubble,
                    isSending && { opacity: 0.7 },
                    isError && { backgroundColor: '#FADBD8', borderColor: colors.danger, borderWidth: 1 }
                ]}>
                    {!isMine && (
                        <Text style={styles.senderName}>{item.senderName}</Text>
                    )}
                    <Text style={[styles.messageText, isMine && styles.myMessageText]}>
                        {item.content}
                    </Text>
                    <View style={styles.metaRow}>
                        <Text style={[styles.timestamp, isMine && styles.myTimestamp]}>
                            {formatTime(item.createdAt)}
                        </Text>
                        {isMine && !isSending && !isError && (
                            <Text style={styles.readReceipt}>
                                {item.isRead ? '✓✓' : '✓'}
                            </Text>
                        )}
                        {isSending && (
                            <Ionicons name="time-outline" size={10} color={isMine ? 'rgba(255,255,255,0.7)' : colors.textTertiary} />
                        )}
                        {isError && (
                            <Ionicons name="alert-circle" size={12} color={colors.danger} />
                        )}
                    </View>
                </View>
            </View>
        );
    };

    if (loading) {
        return (
            <View style={styles.container}>
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                        <Ionicons name="arrow-back" size={24} color={colors.text} />
                    </TouchableOpacity>
                    <View style={styles.headerInfo}>
                        <Text style={styles.headerName} numberOfLines={1}>{participantName}</Text>
                        <Text style={styles.headerSubtitle} numberOfLines={1}>🅿️ {parkingTitle}</Text>
                    </View>
                </View>
                <View style={{ flex: 1, paddingTop: 10 }}>
                    <MessagesSkeleton />
                </View>
            </View>
        );
    }

    return (
        <KeyboardAvoidingView
            style={styles.container}
            behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
            keyboardVerticalOffset={Platform.OS === 'ios' ? 10 : 0}
        >
            {/* Header */}
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <View style={styles.headerInfo}>
                    <Text style={styles.headerName} numberOfLines={1}>{participantName}</Text>
                    <Text style={styles.headerSubtitle} numberOfLines={1}>🅿️ {parkingTitle}</Text>
                </View>
            </View>

            {/* Messages */}
            <FlatList
                ref={flatListRef}
                data={messages}
                renderItem={renderMessage}
                keyExtractor={(item) => item.id}
                contentContainerStyle={styles.messagesList}
                onContentSizeChange={() => flatListRef.current?.scrollToEnd({ animated: false })}
                ListEmptyComponent={
                    <View style={styles.centered}>
                        <Text style={{ fontSize: 32, marginBottom: 8 }}>👋</Text>
                        <Text style={styles.emptyText}>Start the conversation!</Text>
                    </View>
                }
            />

            {/* Input */}
            <View style={[styles.inputContainer, { paddingBottom: Math.max(insets.bottom, 8) }]}>
                <TextInput
                    style={styles.input}
                    value={newMessage}
                    onChangeText={setNewMessage}
                    placeholder="Type a message..."
                    placeholderTextColor={colors.textTertiary}
                    maxLength={2000}
                    multiline
                    editable={!sending}
                />
                <TouchableOpacity
                    style={[styles.sendBtn, (!newMessage.trim() || sending) && styles.sendBtnDisabled]}
                    onPress={handleSend}
                    disabled={!newMessage.trim() || sending}
                >
                    <Ionicons
                        name="send"
                        size={20}
                        color={newMessage.trim() && !sending ? '#fff' : colors.textTertiary}
                    />
                </TouchableOpacity>
            </View>
        </KeyboardAvoidingView>
    );
};

const styles = StyleSheet.create({
    container: { flex: 1, backgroundColor: colors.background },
    centered: { flex: 1, justifyContent: 'center', alignItems: 'center' },
    header: {
        flexDirection: 'row', alignItems: 'center', padding: 12, paddingTop: 50,
        backgroundColor: colors.surface, borderBottomWidth: 1, borderBottomColor: colors.borderLight,
    },
    backBtn: { marginRight: 12 },
    headerInfo: { flex: 1 },
    headerName: { fontSize: 16, fontWeight: '600', color: colors.text },
    headerSubtitle: { fontSize: 12, color: colors.textSecondary, marginTop: 2 },
    messagesList: { padding: 12, paddingBottom: 4 },
    messageBubbleRow: { flexDirection: 'row', marginBottom: 8 },
    messageBubbleRowMine: { justifyContent: 'flex-end' },
    messageBubble: { maxWidth: '75%', padding: 10, borderRadius: 16 },
    myBubble: {
        backgroundColor: colors.primary, borderBottomRightRadius: 4,
    },
    otherBubble: {
        backgroundColor: colors.surface, borderBottomLeftRadius: 4,
        borderWidth: 1, borderColor: colors.borderLight,
    },
    senderName: { fontSize: 11, fontWeight: '600', color: colors.primary, marginBottom: 2 },
    messageText: { fontSize: 15, color: colors.text, lineHeight: 20 },
    myMessageText: { color: '#fff' },
    metaRow: { flexDirection: 'row', justifyContent: 'flex-end', alignItems: 'center', marginTop: 4, gap: 4 },
    timestamp: { fontSize: 10, color: colors.textTertiary },
    myTimestamp: { color: 'rgba(255,255,255,0.7)' },
    readReceipt: { fontSize: 10, color: 'rgba(255,255,255,0.7)' },
    emptyText: { fontSize: 15, color: colors.textTertiary },
    inputContainer: {
        flexDirection: 'row', alignItems: 'flex-end', padding: 8,
        backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.borderLight,
    },
    input: {
        flex: 1, backgroundColor: colors.background, borderRadius: 20,
        paddingHorizontal: 16, paddingVertical: 10, fontSize: 15,
        color: colors.text, maxHeight: 100, marginRight: 8,
        borderWidth: 1, borderColor: colors.borderLight,
    },
    sendBtn: {
        width: 40, height: 40, borderRadius: 20,
        backgroundColor: colors.primary, justifyContent: 'center', alignItems: 'center',
    },
    sendBtnDisabled: { backgroundColor: colors.borderLight },
});

export default ChatScreen;
