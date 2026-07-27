
import React from 'react';
import { View } from 'react-native';

export const ShimmerPlaceholder = (props) => <View style={[{ backgroundColor: '#E0E0E0' }, props.style]} />;
export const DetailSkeleton = () => <View style={{ flex: 1, backgroundColor: '#E0E0E0' }} />;
