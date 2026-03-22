/**
 * Redux Store Configuration
 */

import { configureStore } from '@reduxjs/toolkit';
import authReducer from './slices/authSlice';
import parkingReducer from './slices/parkingSlice';
import bookingReducer from './slices/bookingSlice';
import dashboardReducer from './slices/dashboardSlice';
import reviewReducer from './slices/reviewSlice';
import chatReducer from './slices/chatSlice';
import notificationReducer from './slices/notificationSlice';
import vehicleReducer from './slices/vehicleSlice';
import favoriteReducer from './slices/favoriteSlice';
import paymentReducer from './slices/paymentSlice';
import uiReducer from './slices/uiSlice';

export const store = configureStore({
    reducer: {
        auth: authReducer,
        parking: parkingReducer,
        booking: bookingReducer,
        dashboard: dashboardReducer,
        review: reviewReducer,
        chat: chatReducer,
        notification: notificationReducer,
        vehicle: vehicleReducer,
        favorite: favoriteReducer,
        payment: paymentReducer,
        ui: uiReducer,
    },
    middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware({
            serializableCheck: {
                ignoredActions: ['auth/login/fulfilled', 'auth/register/fulfilled'],
            },
        }),
});

export default store;
