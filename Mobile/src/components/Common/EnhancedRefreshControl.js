
import React, { useState, useCallback } from 'react';
import { RefreshControl } from 'react-native';

export const useEnhancedRefresh = (refreshFunction) => {
    const [refreshing, setRefreshing] = useState(false);
    const [lastRefreshed, setLastRefreshed] = useState(new Date());

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        try {
            await refreshFunction();
            setLastRefreshed(new Date());
        } finally {
            setRefreshing(false);
        }
    }, [refreshFunction]);

    return { refreshing, onRefresh, lastRefreshed };
};

const EnhancedRefreshControl = (props) => <RefreshControl {...props} />;
export default EnhancedRefreshControl;
