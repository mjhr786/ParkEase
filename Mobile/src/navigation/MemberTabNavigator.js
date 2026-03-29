/**
 * Member Tab Navigator
 * Bottom tabs: Explore, Bookings, + (FAB), Map, Profile
 * Matches the mockup with a center floating action button
 */

import React from 'react';
import { View, TouchableOpacity, StyleSheet } from 'react-native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors, typography } from '../styles/globalStyles';

// Screens
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
import MapScreen from '../screens/Map/MapScreen';
import CreateParkingScreen from '../screens/Vendor/CreateParkingScreen';
import MyListingsScreen from '../screens/Vendor/MyListingsScreen';
import VendorBookingsScreen from '../screens/Vendor/VendorBookingsScreen';

const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

const stackOptions = {
    headerShown: false,
    animation: 'slide_from_right',
};

// Explore Stack — main home/search with deep navigation
const ExploreStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Search" component={SearchScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookParking" component={BookingScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="PaymentScreen" component={PaymentScreen} />
        <Stack.Screen name="CreateReview" component={CreateReviewScreen} />
        <Stack.Screen name="ReviewsList" component={ReviewsListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
        <Stack.Screen name="Favorites" component={FavoritesScreen} />
        <Stack.Screen name="Notifications" component={NotificationsScreen} />
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
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
    </Stack.Navigator>
);

// Create Listing Stack (center FAB)
const CreateStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="CreateParking" component={CreateParkingScreen} />
    </Stack.Navigator>
);

// Map Stack
const MapStack = () => (
    <Stack.Navigator screenOptions={stackOptions}>
        <Stack.Screen name="Map" component={MapScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookParking" component={BookingScreen} />
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
        <Stack.Screen name="MyListings" component={MyListingsScreen} />
        <Stack.Screen name="VendorBookings" component={VendorBookingsScreen} />
        <Stack.Screen name="ParkingDetail" component={ParkingDetailScreen} />
        <Stack.Screen name="BookingDetail" component={BookingDetailScreen} />
        <Stack.Screen name="ConversationList" component={ConversationListScreen} />
        <Stack.Screen name="ChatScreen" component={ChatScreen} />
    </Stack.Navigator>
);

/**
 * Center FAB button for create listing
 */
const CenterFabButton = ({ onPress }) => (
    <TouchableOpacity style={fabStyles.container} onPress={onPress} activeOpacity={0.85}>
        <View style={fabStyles.button}>
            <Ionicons name="add" size={28} color={colors.white} />
        </View>
    </TouchableOpacity>
);

const fabStyles = StyleSheet.create({
    container: {
        top: -22,
        justifyContent: 'center',
        alignItems: 'center',
    },
    button: {
        width: 56,
        height: 56,
        borderRadius: 28,
        backgroundColor: colors.primary,
        justifyContent: 'center',
        alignItems: 'center',
        shadowColor: colors.primary,
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.35,
        shadowRadius: 8,
        elevation: 8,
    },
});

const MemberTabNavigator = () => {
    const insets = useSafeAreaInsets();

    return (
        <Tab.Navigator
            screenOptions={({ route }) => ({
                headerShown: false,
                tabBarIcon: ({ focused, color, size }) => {
                    const icons = {
                        ExploreTab: focused ? 'home' : 'home-outline',
                        BookingsTab: focused ? 'bookmark' : 'bookmark-outline',
                        CreateTab: 'add',
                        MapTab: focused ? 'location' : 'location-outline',
                        ProfileTab: focused ? 'person' : 'person-outline',
                    };
                    return <Ionicons name={icons[route.name]} size={size} color={color} />;
                },
                tabBarActiveTintColor: colors.primary,
                tabBarInactiveTintColor: colors.textTertiary,
                tabBarStyle: {
                    backgroundColor: colors.surface,
                    borderTopColor: colors.borderLight,
                    borderTopWidth: 1,
                    paddingBottom: Math.max(insets.bottom, 6),
                    paddingTop: 8,
                    height: 64 + Math.max(insets.bottom, 0),
                },
                tabBarLabelStyle: {
                    fontSize: 11,
                    fontWeight: '600',
                    marginTop: 2,
                },
            })}
        >
            <Tab.Screen
                name="ExploreTab"
                component={ExploreStack}
                options={{ tabBarLabel: 'Explore' }}
            />
            <Tab.Screen
                name="BookingsTab"
                component={BookingsStack}
                options={{ tabBarLabel: 'Bookings' }}
            />
            <Tab.Screen
                name="CreateTab"
                component={CreateStack}
                options={{
                    tabBarLabel: () => null,
                    tabBarIcon: () => null,
                    tabBarButton: (props) => <CenterFabButton {...props} />,
                }}
            />
            <Tab.Screen
                name="MapTab"
                component={MapStack}
                options={{ tabBarLabel: 'Map' }}
            />
            <Tab.Screen
                name="ProfileTab"
                component={ProfileStack}
                options={{ tabBarLabel: 'Profile' }}
            />
        </Tab.Navigator>
    );
};

export default MemberTabNavigator;
