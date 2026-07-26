const fs = require('fs');
const path = require('path');

const writeStub = (filePath, content) => {
    const fullPath = path.join(__dirname, 'Mobile', 'src', filePath);
    fs.mkdirSync(path.dirname(fullPath), { recursive: true });
    fs.writeFileSync(fullPath, content);
};

const reactScreenStub = (name) => `
import React from 'react';
import { View, Text } from 'react-native';

const ${name} = () => (
    <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <Text>${name} - Under Construction</Text>
    </View>
);

export default ${name};
`;

const sliceStub = (name) => `
import { createSlice } from '@reduxjs/toolkit';

const ${name} = createSlice({
    name: '${name.replace('Slice', '')}',
    initialState: {},
    reducers: {}
});

export default ${name}.reducer;
`;

// Screens
writeStub('screens/Home/UnifiedDashboardScreen.js', reactScreenStub('UnifiedDashboardScreen'));
writeStub('screens/Review/ReviewsListScreen.js', reactScreenStub('ReviewsListScreen'));
writeStub('screens/Profile/EditProfileScreen.js', reactScreenStub('EditProfileScreen'));
writeStub('screens/Profile/ChangePasswordScreen.js', reactScreenStub('ChangePasswordScreen'));
writeStub('screens/Notifications/NotificationsScreen.js', reactScreenStub('NotificationsScreen'));
writeStub('screens/Vehicles/VehiclesScreen.js', reactScreenStub('VehiclesScreen'));
writeStub('screens/Favorites/FavoritesScreen.js', reactScreenStub('FavoritesScreen'));

// Components
writeStub('components/Common/EnhancedRefreshControl.js', `
import React from 'react';
import { RefreshControl } from 'react-native';

const EnhancedRefreshControl = (props) => <RefreshControl {...props} />;
export default EnhancedRefreshControl;
`);

writeStub('components/Common/ShimmerPlaceholder.js', `
import React from 'react';
import { View } from 'react-native';

export const ShimmerPlaceholder = (props) => <View style={[{ backgroundColor: '#E0E0E0' }, props.style]} />;
export const DetailSkeleton = () => <View style={{ flex: 1, backgroundColor: '#E0E0E0' }} />;
`);

// Slices/Services
writeStub('store/slices/notificationSlice.js', sliceStub('notificationSlice'));
writeStub('store/slices/chatSlice.js', sliceStub('chatSlice'));
writeStub('services/notifications/NotificationService.js', `
export default {
    initialize: () => {},
    onNotification: () => {}
};
`);
