/**
 * AppTabNavigator (Unified)
 * Bottom tabs: Home, Search, Listings, Bookings, Messages, Profile
 * No role-based routing — every user has access to everything.
 */

import React from 'react';
import { View, Text, AppState } from 'react-native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useSelector, useDispatch } from 'react-redux';
import { colors } from '../styles/globalStyles';

// Thunks for sync
import { getNotificationsThunk } from '../store/slices/notificationSlice';
import { getUnreadCountThunk } from '../store/slices/chatSlice';
import { getMemberDashboardThunk, getVendorDashboardThunk } from '../store/slices/dashboardSlice';

// Screens
import UnifiedDashboardScreen from '../screens/Home/UnifiedDashboardScreen';
import SearchScreen from '../screens/Search/SearchScreen';
import ParkingDetailScreen from '../screens/Search/ParkingDetailScreen';
import BookingScreen from '../screens/Booking/BookingScreen';
import MyBookingsScreen from '../screens/Booking/MyBookingsScreen';
import BookingDetailScreen from '../screens/Booking/BookingDetailScreen';
import PaymentScreen from '../screens/Payment/PaymentScreen';
import MyListingsScreen from '../screens/Vendor/MyListingsScreen';
import CreateParkingScreen from '../screens/Vendor/CreateParkingScreen';
import VendorBookingsScreen from '../screens/Vendor/VendorBookingsScreen';
import CreateReviewScreen from '../screens/Review/CreateReviewScreen';
import ReviewsListScreen from '../screens/Review/ReviewsListScreen';
import ProfileScreen from '../screens/Profile/ProfileScreen';
import EditProfileScreen from '../screens/Profile/EditProfileScreen';
import ChangePasswordScreen from '../screens/Profile/ChangePasswordScreen';
import NotificationsScreen from '../screens/Notifications/NotificationsScreen';
import VehiclesScreen from '../screens/Vehicles/VehiclesScreen';
import FavoritesScreen from '../screens/Favorites/FavoritesScreen';
import ConversationListScreen from '../screens/Chat/ConversationListScreen';
import ChatScreen from '../screens/Chat/ChatScreen';

const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

const stackOptions = {
    headerShown: false,
    animation: 'slide_from_right',
};

// ── Home Stack ──
const HomeStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Dashboard" component={UnifiedDashboardScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookParking" component={BookingScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="PaymentScreen" component={PaymentScreen} />
        <Stack.Screen name="Favorites" component={FavoritesScreen} />
        <Stack.Screen name="Notifications" component={NotificationsScreen} />
        <Stack.Screen name="Vehicles" component={VehiclesScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="ReviewsList" component={ReviewsListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
        <Stack.Screen name="CreateParking" component={CreateParkingScreen} />
    </Stack.Navigator>
);

// ── Search Stack ──
const SearchStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Search" component={SearchScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookParking" component={BookingScreen} />
        <Stack.Screen name="PaymentScreen" component={PaymentScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="ReviewsList" component={ReviewsListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
        <Stack.Screen name="CreateParking" component={CreateParkingScreen} />
        <Stack.Screen name="Vehicles" component={VehiclesScreen} />
    </Stack.Navigator>
);

// ── My Listings Stack ──
const ListingsStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MyListings" component={MyListingsScreen} />
        <Stack.Screen name="CreateParking" component={CreateParkingScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="ReviewsList" component={ReviewsListScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="Vehicles" component={VehiclesScreen} />
    </Stack.Navigator>
);

// ── Bookings Stack ──
const BookingsStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MyBookings" component={MyBookingsScreen} options={{ animation: 'none' }} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="PaymentScreen" component={PaymentScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
        <Stack.Screen name="IncomingBookings" component={VendorBookingsScreen} options={{ animation: 'none' }} />
        <Stack.Screen name="Vehicles" component={VehiclesScreen} />
    </Stack.Navigator>
);

// ── Messages Stack ──
const MessagesStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="ConversationList" component={ConversationListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

// ── Profile Stack ──
const ProfileStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Profile" component={ProfileScreen} />
        <Stack.Screen name="EditProfile" component={EditProfileScreen} />
        <Stack.Screen name="ChangePassword" component={ChangePasswordScreen} />
        <Stack.Screen name="Vehicles" component={VehiclesScreen} />
        <Stack.Screen name="Favorites" component={FavoritesScreen} />
        <Stack.Screen name="Notifications" component={NotificationsScreen} />
        <Stack.Screen name="PaymentScreen" component={PaymentScreen} />
        <Stack.Screen name="MyListings" component={MyListingsScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

const AppTabNavigator = () => {
    const dispatch = useDispatch();
    const insets = useSafeAreaInsets();
    const { unreadCount: notificationUnreadCount } = useSelector((s) => s.notification);
    const { unreadCount: messageUnreadCount } = useSelector((s) => s.chat);

    const refreshCounts = React.useCallback(() => {
        dispatch(getNotificationsThunk());
        dispatch(getUnreadCountThunk());
        // Also refresh dashboard stats to keep everything in sync
        dispatch(getMemberDashboardThunk());
        dispatch(getVendorDashboardThunk());
    }, [dispatch]);

    React.useEffect(() => {
        // Initial fetch
        refreshCounts();

        // 1. Listen for app state changes (background -> foreground)
        const subscription = AppState.addEventListener('change', (nextAppState) => {
            if (nextAppState === 'active') {
                refreshCounts();
            }
        });

        // 2. Periodic polling (every 60 seconds)
        const interval = setInterval(refreshCounts, 60000);

        return () => {
            subscription.remove();
            clearInterval(interval);
        };
    }, [refreshCounts]);

    return (
        <Tab.Navigator
            screenOptions={({ route }) => ({
                headerShown: false,
                tabBarIcon: ({ focused, color, size }) => {
                    const icons = {
                        HomeTab: focused ? 'home' : 'home-outline',
                        SearchTab: focused ? 'search' : 'search-outline',
                        ListingsTab: focused ? 'location' : 'location-outline',
                        BookingsTab: focused ? 'calendar' : 'calendar-outline',
                        MessagesTab: focused ? 'chatbubbles' : 'chatbubbles-outline',
                        ProfileTab: focused ? 'person' : 'person-outline',
                    };
                    return <Ionicons name={icons[route.name]} size={size} color={color} />;
                },
                tabBarActiveTintColor: colors.primary,
                tabBarInactiveTintColor: colors.textTertiary,
                tabBarStyle: {
                    backgroundColor: colors.surface,
                    borderTopColor: colors.borderLight,
                    paddingBottom: Math.max(insets.bottom, 6),
                    paddingTop: 6,
                    height: 60 + Math.max(insets.bottom, 0),
                },
                tabBarLabelStyle: {
                    fontSize: 10,
                    fontWeight: '500',
                },
            })}
        >
            <Tab.Screen 
                name="HomeTab" 
                component={HomeStack} 
                options={{ tabBarLabel: 'Home' }} 
                listeners={{ tabPress: refreshCounts }}
            />
            <Tab.Screen 
                name="SearchTab" 
                component={SearchStack} 
                options={{ tabBarLabel: 'Search' }} 
                listeners={{ tabPress: refreshCounts }}
            />
            <Tab.Screen 
                name="ListingsTab" 
                component={ListingsStack} 
                options={{ tabBarLabel: 'Listings' }} 
                listeners={{ tabPress: refreshCounts }}
            />
            <Tab.Screen 
                name="BookingsTab" 
                component={BookingsStack} 
                options={{ tabBarLabel: 'Bookings' }} 
                listeners={{ tabPress: refreshCounts }}
            />
            <Tab.Screen 
                name="MessagesTab" 
                component={MessagesStack} 
                options={{ 
                    tabBarLabel: 'Messages',
                    tabBarBadge: messageUnreadCount > 0 ? messageUnreadCount : null,
                    tabBarBadgeStyle: { backgroundColor: colors.primary, color: colors.white }
                }} 
                listeners={{ tabPress: refreshCounts }}
            />
            <Tab.Screen 
                name="ProfileTab" 
                component={ProfileStack} 
                options={{ 
                    tabBarLabel: 'Profile',
                    tabBarBadge: notificationUnreadCount > 0 ? notificationUnreadCount : null,
                    tabBarBadgeStyle: { backgroundColor: colors.danger, color: colors.white }
                }} 
                listeners={{ tabPress: refreshCounts }}
            />
        </Tab.Navigator>
    );
};

export default AppTabNavigator;
