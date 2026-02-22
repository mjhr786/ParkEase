/**
 * Chat Screen
 * Displays message thread for a conversation with real-time updates
 */

import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
    View, Text, FlatList, TextInput, TouchableOpacity,
    StyleSheet, KeyboardAvoidingView, Platform, ActivityIndicator
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../../styles/globalStyles';
import { useAuth } from '../../contexts/AuthContext';
import chatService from '../../services/chat/chatService';

const ChatScreen = ({ route, navigation }) => {
    const { conversationId, parkingSpaceId, participantName, parkingTitle } = route.params;
    const { user } = useAuth();
    const [messages, setMessages] = useState([]);
    const [newMessage, setNewMessage] = useState('');
    const [loading, setLoading] = useState(true);
    const [sending, setSending] = useState(false);
    const flatListRef = useRef(null);
    const pollInterval = useRef(null);

    useEffect(() => {
        loadMessages();
        markRead();

        // Poll for new messages every 5 seconds (lightweight real-time substitute for mobile)
        pollInterval.current = setInterval(loadMessages, 5000);
        return () => {
            if (pollInterval.current) clearInterval(pollInterval.current);
        };
    }, [conversationId]);

    const loadMessages = async () => {
        try {
            const result = await chatService.getMessages(conversationId);
            if (result.success) {
                // Reverse to show oldest first (API returns newest first)
                setMessages((result.data || []).reverse());
            }
        } catch (error) {
            console.error('Failed to load messages:', error);
        } finally {
            setLoading(false);
        }
    };

    const markRead = async () => {
        try {
            await chatService.markAsRead(conversationId);
        } catch { }
    };

    const handleSend = async () => {
        const content = newMessage.trim();
        if (!content || sending) return;

        setSending(true);
        try {
            const result = await chatService.sendMessage(parkingSpaceId, content);
            if (result.success && result.data) {
                setMessages(prev => [...prev, result.data]);
                setNewMessage('');
                // Auto-scroll
                setTimeout(() => flatListRef.current?.scrollToEnd({ animated: true }), 100);
            }
        } catch (error) {
            console.error('Failed to send message:', error);
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
        return (
            <View style={[styles.messageBubbleRow, isMine && styles.messageBubbleRowMine]}>
                <View style={[styles.messageBubble, isMine ? styles.myBubble : styles.otherBubble]}>
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
                        {isMine && (
                            <Text style={styles.readReceipt}>
                                {item.isRead ? '✓✓' : '✓'}
                            </Text>
                        )}
                    </View>
                </View>
            </View>
        );
    };

    return (
        <KeyboardAvoidingView
            style={styles.container}
            behavior={Platform.OS === 'ios' ? 'padding' : undefined}
            keyboardVerticalOffset={0}
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
            {loading ? (
                <View style={styles.centered}>
                    <ActivityIndicator size="large" color={colors.primary} />
                </View>
            ) : (
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
            )}

            {/* Input */}
            <View style={styles.inputContainer}>
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
        paddingBottom: Platform.OS === 'ios' ? 24 : 8,
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
