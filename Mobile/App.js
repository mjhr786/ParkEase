/**
 * App.js - ParkEase Mobile App Entry Point
 * Wraps the app with providers: Redux, Navigation, StatusBar
 */

import React from 'react';
import { StatusBar } from 'expo-status-bar';
import { Provider } from 'react-redux';
import store from './src/store';
import RootNavigator from './src/navigation/RootNavigator';

export default function App() {
  return (
    <Provider store={store}>
      <StatusBar style="auto" />
      <RootNavigator />
    </Provider>
  );
}
