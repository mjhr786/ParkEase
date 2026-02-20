/**
 * Member Tab Navigator
 * Bottom tabs: Home, Search, Bookings, Profile
 */

import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../styles/globalStyles';

// Screens
import MemberDashboardScreen from '../screens/Member/MemberDashboardScreen';
import SearchScreen from '../screens/Search/SearchScreen';
import ParkingDetailScreen from '../screens/Search/ParkingDetailScreen';
import BookingScreen from '../screens/Booking/BookingScreen';
import MyBookingsScreen from '../screens/Booking/MyBookingsScreen';
import BookingDetailScreen from '../screens/Booking/BookingDetailScreen';
import CreateReviewScreen from '../screens/Review/CreateReviewScreen';
import ProfileScreen from '../screens/Profile/ProfileScreen';

const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

const stackOptions = {
    headerShown: false,
    animation: 'slide_from_right',
};

// Home Stack
const HomeStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MemberDashboard" component={MemberDashboardScreen} />
    </Stack.Navigator>
);

// Search Stack
const SearchStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Search" component={SearchScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookParking" component={BookingScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
    </Stack.Navigator>
);

// Bookings Stack
const BookingsStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="MyBookings" component={MyBookingsScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
    </Stack.Navigator>
);

// Profile Stack
const ProfileStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Profile" component={ProfileScreen} />
    </Stack.Navigator>
);

const MemberTabNavigator = () => (
    <Tab.Navigator
        screenOptions={({ route }) => ({
            headerShown: false,
            tabBarIcon: ({ focused, color, size }) => {
                const icons = {
                    HomeTab: focused ? 'home' : 'home-outline',
                    SearchTab: focused ? 'search' : 'search-outline',
                    BookingsTab: focused ? 'calendar' : 'calendar-outline',
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
        <Tab.Screen name="HomeTab" component={HomeStack} options={{ tabBarLabel: 'Home' }} />
        <Tab.Screen name="SearchTab" component={SearchStack} options={{ tabBarLabel: 'Search' }} />
        <Tab.Screen name="BookingsTab" component={BookingsStack} options={{ tabBarLabel: 'Bookings' }} />
        <Tab.Screen name="ProfileTab" component={ProfileStack} options={{ tabBarLabel: 'Profile' }} />
    </Tab.Navigator>
);

export default MemberTabNavigator;
