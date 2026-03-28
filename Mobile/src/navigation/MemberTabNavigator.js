/**
 * Member Tab Navigator
 * Bottom tabs: Home, Search, Bookings, Messages, Profile
 */

import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors, typography } from '../styles/globalStyles';

// Screens
import MemberDashboardScreen from '../screens/Member/MemberDashboardScreen';
import SearchScreen from '../screens/Search/SearchScreen';
import ParkingDetailScreen from '../screens/Search/ParkingDetailScreen';
import BookingScreen from '../screens/Booking/BookingScreen';
import MyBookingsScreen from '../screens/Booking/MyBookingsScreen';
import PaymentScreen from '../screens/Payment/PaymentScreen';
import BookingDetailScreen from '../screens/Booking/BookingDetailScreen';
import CreateReviewScreen from '../screens/Review/CreateReviewScreen';
import ReviewsListScreen from '../screens/Review/ReviewsListScreen';
import ProfileScreen from '../screens/Profile/ProfileScreen';
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

// Home Stack — supports deep navigation from dashboard tiles
const HomeStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MemberDashboard" component={MemberDashboardScreen} />
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
    </Stack.Navigator>
);

// Search Stack
const SearchStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Search" component={SearchScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookParking" component={BookingScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="ReviewsList" component={ReviewsListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

// Bookings Stack
const BookingsStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MyBookings" component={MyBookingsScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="PaymentScreen" component={PaymentScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

// Profile Stack
const ProfileStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Profile" component={ProfileScreen} />
        <Stack.Screen name="ChangePassword" component={ChangePasswordScreen} />
        <Stack.Screen name="Vehicles" component={VehiclesScreen} />
        <Stack.Screen name="Favorites" component={FavoritesScreen} />
        <Stack.Screen name="Notifications" component={NotificationsScreen} />
    </Stack.Navigator>
);

// Messages Stack
const MessagesStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="ConversationList" component={ConversationListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

const MemberTabNavigator = () => {
    const insets = useSafeAreaInsets();
    
    return (
    <Tab.Navigator
        screenOptions={({ route }) => ({
            headerShown: false,
            tabBarIcon: ({ focused, color, size }) => {
                const icons = {
                    HomeTab: focused ? 'home' : 'home-outline',
                    SearchTab: focused ? 'search' : 'search-outline',
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
                fontSize: 11,
                fontWeight: '500',
            },
        })}
    >
        <Tab.Screen name="HomeTab" component={HomeStack} options={{ tabBarLabel: 'Home' }} />
        <Tab.Screen name="SearchTab" component={SearchStack} options={{ tabBarLabel: 'Search' }} />
        <Tab.Screen name="BookingsTab" component={BookingsStack} options={{ tabBarLabel: 'Bookings' }} />
        <Tab.Screen name="MessagesTab" component={MessagesStack} options={{ tabBarLabel: 'Messages' }} />
        <Tab.Screen name="ProfileTab" component={ProfileStack} options={{ tabBarLabel: 'Profile' }} />
    </Tab.Navigator>
    );
};

export default MemberTabNavigator;
