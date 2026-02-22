/**
 * Vendor Tab Navigator
 * Bottom tabs: Dashboard, Listings, Bookings, Profile
 */

import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../styles/globalStyles';

// Screens
import VendorDashboardScreen from '../screens/Vendor/VendorDashboardScreen';
import MyListingsScreen from '../screens/Vendor/MyListingsScreen';
import CreateParkingScreen from '../screens/Vendor/CreateParkingScreen';
import VendorBookingsScreen from '../screens/Vendor/VendorBookingsScreen';
import BookingDetailScreen from '../screens/Booking/BookingDetailScreen';
import ProfileScreen from '../screens/Profile/ProfileScreen';
import ConversationListScreen from '../screens/Chat/ConversationListScreen';
import ChatScreen from '../screens/Chat/ChatScreen';

const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

const stackOptions = {
    headerShown: false,
    animation: 'slide_from_right',
};

// Dashboard Stack
const DashboardStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="VendorDashboard" component={VendorDashboardScreen} />
    </Stack.Navigator>
);

// Listings Stack
const ListingsStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MyListings" component={MyListingsScreen} />
        <Stack.Screen name="CreateParking" component={CreateParkingScreen} />
    </Stack.Navigator>
);

// Bookings Stack
const BookingsStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="VendorBookings" component={VendorBookingsScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
    </Stack.Navigator>
);

// Profile Stack
const ProfileStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Profile" component={ProfileScreen} />
    </Stack.Navigator>
);

// Messages Stack
const MessagesStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="ConversationList" component={ConversationListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

const VendorTabNavigator = () => (
    <Tab.Navigator
        screenOptions={({ route }) => ({
            headerShown: false,
            tabBarIcon: ({ focused, color, size }) => {
                const icons = {
                    DashboardTab: focused ? 'analytics' : 'analytics-outline',
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
                paddingBottom: 6,
                paddingTop: 6,
                height: 60,
            },
            tabBarLabelStyle: {
                fontSize: 11,
                fontWeight: '500',
            },
        })}
    >
        <Tab.Screen name="DashboardTab" component={DashboardStack} options={{ tabBarLabel: 'Dashboard' }} />
        <Tab.Screen name="ListingsTab" component={ListingsStack} options={{ tabBarLabel: 'Listings' }} />
        <Tab.Screen name="BookingsTab" component={BookingsStack} options={{ tabBarLabel: 'Bookings' }} />
        <Tab.Screen name="MessagesTab" component={MessagesStack} options={{ tabBarLabel: 'Messages' }} />
        <Tab.Screen name="ProfileTab" component={ProfileStack} options={{ tabBarLabel: 'Profile' }} />
    </Tab.Navigator>
);

export default VendorTabNavigator;
