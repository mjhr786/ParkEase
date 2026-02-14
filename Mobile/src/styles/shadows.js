/**
 * Shadow Presets
 * Cross-platform elevation shadows
 */

import { Platform } from 'react-native';

const createShadow = (elevation, color = '#000') => {
    if (Platform.OS === 'android') {
        return { elevation };
    }

    const shadowMap = {
        1: { shadowOffset: { width: 0, height: 1 }, shadowOpacity: 0.05, shadowRadius: 2 },
        2: { shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.08, shadowRadius: 4 },
        3: { shadowOffset: { width: 0, height: 4 }, shadowOpacity: 0.1, shadowRadius: 8 },
        4: { shadowOffset: { width: 0, height: 6 }, shadowOpacity: 0.12, shadowRadius: 12 },
        5: { shadowOffset: { width: 0, height: 8 }, shadowOpacity: 0.15, shadowRadius: 16 },
    };

    return {
        shadowColor: color,
        ...(shadowMap[elevation] || shadowMap[2]),
    };
};

export const shadows = {
    none: {},
    sm: createShadow(1),
    md: createShadow(2),
    lg: createShadow(3),
    xl: createShadow(4),
    '2xl': createShadow(5),
    card: createShadow(2),
    button: createShadow(3),
    modal: createShadow(5),
};

export default shadows;
