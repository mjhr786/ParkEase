# React Native App Development - Complete Guidelines & Architecture

## Table of Contents
1. [Project Setup & Structure](#project-setup--structure)
2. [Architectural Patterns](#architectural-patterns)
3. [Code Style & Conventions](#code-style--conventions)
4. [Component Design Patterns](#component-design-patterns)
5. [State Management](#state-management)
6. [Navigation Architecture](#navigation-architecture)
7. [Error Handling & Logging](#error-handling--logging)
8. [Testing Strategy](#testing-strategy)
9. [Performance Optimization](#performance-optimization)
10. [Security Best Practices](#security-best-practices)
11. [Development Workflow](#development-workflow)
12. [Documentation Standards](#documentation-standards)

---

## Project Setup & Structure

### Project Initialization
```bash
# Use Expo or React Native CLI for setup
npx create-expo-app MyApp
# OR
npx react-native init MyApp

# Install essential dependencies
npm install @react-navigation/native @react-navigation/bottom-tabs
npm install redux @reduxjs/toolkit react-redux
npm install axios
npm install dotenv
npm install eslint prettier @react-native/eslint-config --save-dev
```

### Optimal Project Structure
```
MyApp/
├── src/
│   ├── assets/
│   │   ├── fonts/
│   │   ├── images/
│   │   └── icons/
│   │
│   ├── components/
│   │   ├── Common/
│   │   │   ├── Button.js
│   │   │   ├── Card.js
│   │   │   ├── Header.js
│   │   │   └── __tests__/
│   │   │
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   │   ├── LoginForm.js
│   │   │   │   └── __tests__/
│   │   │   └── Profile/
│   │   │       ├── ProfileCard.js
│   │   │       └── __tests__/
│   │   │
│   │   └── Layouts/
│   │       ├── ScreenLayout.js
│   │       └── AuthLayout.js
│   │
│   ├── screens/
│   │   ├── Auth/
│   │   │   ├── LoginScreen.js
│   │   │   ├── SignupScreen.js
│   │   │   └── ForgotPasswordScreen.js
│   │   │
│   │   ├── Home/
│   │   │   ├── HomeScreen.js
│   │   │   └── DetailsScreen.js
│   │   │
│   │   └── Profile/
│   │       ├── ProfileScreen.js
│   │       └── SettingsScreen.js
│   │
│   ├── navigation/
│   │   ├── RootNavigator.js
│   │   ├── AuthNavigator.js
│   │   ├── AppNavigator.js
│   │   └── LinkingConfiguration.js
│   │
│   ├── services/
│   │   ├── api/
│   │   │   ├── apiClient.js
│   │   │   ├── endpoints.js
│   │   │   └── interceptors.js
│   │   │
│   │   ├── storage/
│   │   │   └── asyncStorage.js
│   │   │
│   │   ├── auth/
│   │   │   └── authService.js
│   │   │
│   │   └── notification/
│   │       └── notificationService.js
│   │
│   ├── store/
│   │   ├── index.js
│   │   ├── slices/
│   │   │   ├── authSlice.js
│   │   │   ├── userSlice.js
│   │   │   └── uiSlice.js
│   │   │
│   │   └── thunks/
│   │       ├── authThunks.js
│   │       └── userThunks.js
│   │
│   ├── hooks/
│   │   ├── useAuth.js
│   │   ├── useUser.js
│   │   ├── useFetch.js
│   │   └── useLocalStorage.js
│   │
│   ├── utils/
│   │   ├── validators.js
│   │   ├── formatters.js
│   │   ├── constants.js
│   │   ├── errorHandler.js
│   │   └── logger.js
│   │
│   ├── styles/
│   │   ├── colors.js
│   │   ├── spacing.js
│   │   ├── typography.js
│   │   ├── shadows.js
│   │   └── globalStyles.js
│   │
│   ├── config/
│   │   ├── environment.js
│   │   └── appConfig.js
│   │
│   └── App.js
│
├── __tests__/
│   ├── setup.js
│   ├── fixtures/
│   └── utils/
│
├── .env.example
├── .eslintrc.json
├── .prettierrc.json
├── jest.config.js
├── tsconfig.json (if using TypeScript)
├── app.json
└── package.json
```

---

## Architectural Patterns

### 1. Clean Architecture Principles

Follow the Clean Architecture pattern with clear separation of concerns:

```
Presentation Layer (Screens & Components)
         ↓
  Business Logic Layer (Services & Hooks)
         ↓
   Data Layer (Redux Store)
         ↓
   External Services (API, Storage)
```

### 2. MVVM Pattern for Screens

Each screen follows Model-View-ViewModel structure:

```javascript
// ViewModel Hook
export const useLoginViewModel = () => {
  const dispatch = useDispatch();
  const { loading, error } = useSelector(state => state.auth);
  const [formData, setFormData] = useState({ email: '', password: '' });

  const handleLogin = useCallback(async () => {
    await dispatch(loginThunk(formData));
  }, [formData, dispatch]);

  return {
    formData,
    setFormData,
    loading,
    error,
    handleLogin,
  };
};

// View Component
const LoginScreen = () => {
  const { formData, setFormData, loading, error, handleLogin } = useLoginViewModel();
  
  return (
    <SafeAreaView>
      {/* JSX */}
    </SafeAreaView>
  );
};
```

### 3. Dependency Injection Pattern

```javascript
// services/serviceContainer.js
export const createServiceContainer = (config) => {
  const apiClient = createApiClient(config.apiBaseUrl);
  
  return {
    authService: new AuthService(apiClient),
    userService: new UserService(apiClient),
    storageService: new StorageService(),
  };
};

// App.js
const services = createServiceContainer(environment.config);
```

---

## Code Style & Conventions

### 1. ESLint Configuration

```json
// .eslintrc.json
{
  "root": true,
  "extends": [
    "@react-native-community",
    "prettier"
  ],
  "parser": "@babel/eslint-parser",
  "parserOptions": {
    "ecmaVersion": 2021,
    "sourceType": "module",
    "ecmaFeatures": {
      "jsx": true
    }
  },
  "plugins": [
    "react",
    "react-native",
    "react-hooks"
  ],
  "rules": {
    "react/prop-types": "warn",
    "react-hooks/rules-of-hooks": "error",
    "react-hooks/exhaustive-deps": "warn",
    "no-console": ["warn", { "allow": ["warn", "error"] }],
    "no-unused-vars": ["error", { "argsIgnorePattern": "^_" }],
    "prefer-const": "error",
    "eqeqeq": ["error", "always"],
    "curly": "error"
  }
}
```

### 2. Prettier Configuration

```json
// .prettierrc.json
{
  "semi": true,
  "trailingComma": "es5",
  "singleQuote": true,
  "printWidth": 100,
  "tabWidth": 2,
  "useTabs": false,
  "arrowParens": "always",
  "bracketSpacing": true,
  "jsxBracketSameLine": false
}
```

### 3. Naming Conventions

**Files and Directories:**
- PascalCase for component files: `LoginForm.js`, `UserProfile.js`
- camelCase for utility/service files: `apiClient.js`, `validators.js`
- Index files for exports: `index.js` in each folder

**Variables and Functions:**
- camelCase for variables and regular functions: `const userName = ''`
- PascalCase for React components: `function LoginScreen() {}`
- UPPER_SNAKE_CASE for constants: `const MAX_RETRIES = 3`
- Leading underscore for unused variables: `const _unused = value`

**Hooks:**
- Custom hooks always start with `use`: `useAuth()`, `useFetch()`

**Tests:**
- Test files in `__tests__` folder with `.test.js` suffix
- Descriptive test names: `describe('UserProfile Component', () => {})`

### 4. File Format Standards

```javascript
// Correct file order: imports → types → component → exports

// 1. External imports
import React, { useState, useCallback } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';

// 2. Internal imports
import Button from '@components/Common/Button';
import { useAuth } from '@hooks/useAuth';
import { colors, spacing } from '@styles/index';

// 3. Type definitions (if using TypeScript)
interface Props {
  title: string;
  onPress: () => void;
}

// 4. Component
const MyComponent = ({ title, onPress }: Props) => {
  const [state, setState] = useState(false);
  
  return <View style={styles.container}>{/* JSX */}</View>;
};

// 5. Styles
const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: spacing.medium,
  },
});

// 6. Exports
export default MyComponent;
```

---

## Component Design Patterns

### 1. Functional Component Pattern

```javascript
/**
 * Button Component
 * A reusable button with multiple variants and states
 * 
 * @component
 * @example
 * <Button 
 *   title="Login" 
 *   variant="primary" 
 *   loading={false}
 *   onPress={() => handleLogin()}
 * />
 */
const Button = React.memo(({
  title,
  onPress,
  variant = 'primary',
  loading = false,
  disabled = false,
  ...props
}) => {
  const styles = getStyles(variant);

  return (
    <TouchableOpacity
      disabled={disabled || loading}
      onPress={onPress}
      activeOpacity={0.7}
      style={[styles.button, disabled && styles.disabled]}
      {...props}
    >
      {loading ? (
        <ActivityIndicator color={colors.white} />
      ) : (
        <Text style={styles.text}>{title}</Text>
      )}
    </TouchableOpacity>
  );
});

Button.displayName = 'Button';

export default Button;
```

### 2. Smart/Container Component Pattern

```javascript
/**
 * UserProfileContainer
 * Connects UI component with Redux store and business logic
 */
const UserProfileContainer = ({ userId }) => {
  const dispatch = useDispatch();
  const { user, loading, error } = useSelector(selectUserById(userId));

  useEffect(() => {
    dispatch(fetchUserThunk(userId));
  }, [userId, dispatch]);

  if (loading) return <LoadingScreen />;
  if (error) return <ErrorScreen error={error} />;

  return <UserProfile user={user} onRefresh={() => dispatch(fetchUserThunk(userId))} />;
};
```

### 3. Presentational Component Pattern

```javascript
/**
 * Pure presentation component - no business logic
 */
const UserProfile = ({ user, onRefresh }) => {
  return (
    <ScrollView>
      <Header title={user.name} onRefresh={onRefresh} />
      <Avatar source={{ uri: user.avatar }} />
      <Text>{user.bio}</Text>
    </ScrollView>
  );
};
```

### 4. Compound Component Pattern

```javascript
// Parent component with sub-components
const Card = ({ children }) => <View style={styles.card}>{children}</View>;
const CardHeader = ({ title }) => <Text style={styles.header}>{title}</Text>;
const CardBody = ({ children }) => <View style={styles.body}>{children}</View>;
const CardFooter = ({ children }) => <View style={styles.footer}>{children}</View>;

Card.Header = CardHeader;
Card.Body = CardBody;
Card.Footer = CardFooter;

// Usage
<Card>
  <Card.Header title="Title" />
  <Card.Body>Content</Card.Body>
  <Card.Footer>Action</Card.Footer>
</Card>
```

### 5. HOC (Higher-Order Component) Pattern

```javascript
/**
 * withTheme HOC for theming support
 */
const withTheme = (Component) => {
  return (props) => {
    const theme = useSelector(selectTheme);
    return <Component {...props} theme={theme} />;
  };
};

// Usage
export default withTheme(MyComponent);
```

---

## State Management

### Redux with Redux Toolkit

#### Store Configuration

```javascript
// store/index.js
import { configureStore } from '@reduxjs/toolkit';
import authReducer from './slices/authSlice';
import userReducer from './slices/userSlice';
import uiReducer from './slices/uiSlice';

export const store = configureStore({
  reducer: {
    auth: authReducer,
    user: userReducer,
    ui: uiReducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: {
        ignoredActions: ['auth/loginFulfilled'],
      },
    }),
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
```

#### Slice Creation

```javascript
// store/slices/authSlice.js
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import * as authService from '@services/auth/authService';

/**
 * Async thunk for login
 */
export const loginThunk = createAsyncThunk(
  'auth/login',
  async (credentials, { rejectWithValue }) => {
    try {
      const response = await authService.login(credentials);
      return response.data;
    } catch (error) {
      return rejectWithValue(error.response?.data?.message);
    }
  }
);

const initialState = {
  user: null,
  token: null,
  loading: false,
  error: null,
  isAuthenticated: false,
};

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    logout: (state) => {
      state.user = null;
      state.token = null;
      state.isAuthenticated = false;
      state.error = null;
    },
    clearError: (state) => {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(loginThunk.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(loginThunk.fulfilled, (state, action) => {
        state.loading = false;
        state.user = action.payload.user;
        state.token = action.payload.token;
        state.isAuthenticated = true;
      })
      .addCase(loginThunk.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
        state.isAuthenticated = false;
      });
  },
});

export const { logout, clearError } = authSlice.actions;
export default authSlice.reducer;
```

#### Custom Hooks for Store

```javascript
// hooks/useAuth.js
import { useDispatch, useSelector } from 'react-redux';
import { useCallback } from 'react';
import { loginThunk, logout } from '@store/slices/authSlice';

export const useAuth = () => {
  const dispatch = useDispatch();
  const { user, token, loading, error, isAuthenticated } = useSelector(
    (state) => state.auth
  );

  const login = useCallback(
    async (credentials) => {
      return dispatch(loginThunk(credentials));
    },
    [dispatch]
  );

  const handleLogout = useCallback(() => {
    dispatch(logout());
  }, [dispatch]);

  return {
    user,
    token,
    loading,
    error,
    isAuthenticated,
    login,
    logout: handleLogout,
  };
};
```

---

## Navigation Architecture

### Root Navigator

```javascript
// navigation/RootNavigator.js
import React, { useEffect } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useAuth } from '@hooks/useAuth';
import AuthNavigator from './AuthNavigator';
import AppNavigator from './AppNavigator';
import SplashScreen from '@screens/Auth/SplashScreen';

const Stack = createNativeStackNavigator();

/**
 * RootNavigator
 * Conditionally renders AuthNavigator or AppNavigator based on authentication state
 */
const RootNavigator = () => {
  const { isAuthenticated, loading } = useAuth();

  if (loading) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <ActivityIndicator size="large" />
      </View>
    );
  }

  return (
    <NavigationContainer linking={linking} fallback={<SplashScreen />}>
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {isAuthenticated ? (
          <Stack.Screen name="App" component={AppNavigator} />
        ) : (
          <Stack.Screen name="Auth" component={AuthNavigator} />
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
};

export default RootNavigator;
```

### Deep Linking Configuration

```javascript
// navigation/LinkingConfiguration.js
export const linking = {
  prefixes: ['https://yourapp.com', 'yourapp://'],
  config: {
    screens: {
      // Auth Stack
      Auth: {
        screens: {
          Login: 'login',
          Signup: 'signup',
          ForgotPassword: 'forgot-password',
        },
      },
      // App Stack
      App: {
        screens: {
          Home: 'home',
          UserProfile: 'profile/:userId',
          Details: 'details/:id',
        },
      },
      // Fallback
      NotFound: '*',
    },
  },
};
```

---

## Error Handling & Logging

### Global Error Handler

```javascript
// utils/errorHandler.js
export class AppError extends Error {
  constructor(message, code = 'UNKNOWN_ERROR', statusCode = 500) {
    super(message);
    this.code = code;
    this.statusCode = statusCode;
    this.timestamp = new Date();
  }
}

export const handleError = (error) => {
  if (error instanceof AppError) {
    return {
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
    };
  }

  if (error.response) {
    // Server responded with error status
    return {
      message: error.response.data?.message || 'Server error',
      code: error.response.data?.code || 'SERVER_ERROR',
      statusCode: error.response.status,
    };
  }

  if (error.request) {
    // Request made but no response
    return {
      message: 'Network error. Please check your connection.',
      code: 'NETWORK_ERROR',
      statusCode: 0,
    };
  }

  return {
    message: error.message || 'An unexpected error occurred',
    code: 'UNKNOWN_ERROR',
    statusCode: 500,
  };
};
```

### Logger Utility

```javascript
// utils/logger.js
const LOG_LEVELS = {
  DEBUG: 0,
  INFO: 1,
  WARN: 2,
  ERROR: 3,
};

class Logger {
  constructor(logLevel = LOG_LEVELS.INFO) {
    this.logLevel = logLevel;
  }

  debug(tag, message, data = null) {
    if (this.logLevel <= LOG_LEVELS.DEBUG) {
      console.log(`[DEBUG] ${tag}: ${message}`, data);
    }
  }

  info(tag, message, data = null) {
    if (this.logLevel <= LOG_LEVELS.INFO) {
      console.log(`[INFO] ${tag}: ${message}`, data);
    }
  }

  warn(tag, message, data = null) {
    if (this.logLevel <= LOG_LEVELS.WARN) {
      console.warn(`[WARN] ${tag}: ${message}`, data);
    }
  }

  error(tag, message, error = null) {
    if (this.logLevel <= LOG_LEVELS.ERROR) {
      console.error(`[ERROR] ${tag}: ${message}`, error);
      // Send to error tracking service (e.g., Sentry)
      trackError(tag, message, error);
    }
  }
}

export default new Logger();
```

---

## Testing Strategy

### Component Testing

```javascript
// __tests__/components/Button.test.js
import React from 'react';
import { render, fireEvent } from '@testing-library/react-native';
import Button from '@components/Common/Button';

describe('Button Component', () => {
  it('should render with correct title', () => {
    const { getByText } = render(<Button title="Click Me" onPress={() => {}} />);
    expect(getByText('Click Me')).toBeTruthy();
  });

  it('should call onPress when pressed', () => {
    const onPress = jest.fn();
    const { getByRole } = render(<Button title="Click" onPress={onPress} />);
    fireEvent.press(getByRole('button'));
    expect(onPress).toHaveBeenCalled();
  });

  it('should be disabled when loading', () => {
    const onPress = jest.fn();
    const { getByRole } = render(
      <Button title="Loading" onPress={onPress} loading={true} />
    );
    fireEvent.press(getByRole('button'));
    expect(onPress).not.toHaveBeenCalled();
  });
});
```

### Redux Testing

```javascript
// __tests__/store/authSlice.test.js
import authReducer, { logout, loginThunk } from '@store/slices/authSlice';

describe('Auth Slice', () => {
  const initialState = {
    user: null,
    token: null,
    loading: false,
    error: null,
    isAuthenticated: false,
  };

  it('should handle logout', () => {
    const state = {
      user: { id: 1, name: 'John' },
      token: 'token123',
      isAuthenticated: true,
      loading: false,
      error: null,
    };

    const result = authReducer(state, logout());
    expect(result.isAuthenticated).toBe(false);
    expect(result.user).toBe(null);
  });

  it('should handle loginThunk.pending', () => {
    const result = authReducer(initialState, loginThunk.pending());
    expect(result.loading).toBe(true);
  });
});
```

### Hook Testing

```javascript
// __tests__/hooks/useAuth.test.js
import { renderHook, act } from '@testing-library/react-native';
import { Provider } from 'react-redux';
import { useAuth } from '@hooks/useAuth';
import { store } from '@store/index';

const wrapper = ({ children }) => <Provider store={store}>{children}</Provider>;

describe('useAuth Hook', () => {
  it('should return auth state', () => {
    const { result } = renderHook(() => useAuth(), { wrapper });
    expect(result.current.isAuthenticated).toBe(false);
  });
});
```

---

## Performance Optimization

### 1. Component Memoization

```javascript
import React from 'react';

// Memoize to prevent unnecessary re-renders
const UserCard = React.memo(({ user, onPress }) => {
  return <View>{/* Component */}</View>;
}, (prevProps, nextProps) => {
  // Custom comparison if needed
  return prevProps.user.id === nextProps.user.id;
});
```

### 2. Lazy Loading & Code Splitting

```javascript
// navigation/RootNavigator.js
const HomeScreen = lazy(() => import('@screens/Home/HomeScreen'));
const ProfileScreen = lazy(() => import('@screens/Profile/ProfileScreen'));

<Suspense fallback={<LoadingScreen />}>
  <HomeScreen />
</Suspense>
```

### 3. Optimize Flatlist

```javascript
<FlatList
  data={items}
  keyExtractor={(item) => item.id.toString()}
  renderItem={({ item }) => <Item data={item} />}
  removeClippedSubviews={true}
  maxToRenderPerBatch={10}
  updateCellsBatchingPeriod={50}
  initialNumToRender={10}
  windowSize={10}
  onEndReachedThreshold={0.5}
  onEndReached={() => loadMore()}
/>
```

### 4. Image Optimization

```javascript
import FastImage from 'react-native-fast-image';

<FastImage
  source={{ uri: imageUrl, priority: FastImage.priority.normal }}
  style={{ width: 200, height: 200 }}
  resizeMode={FastImage.resizeMode.contain}
/>
```

---

## Security Best Practices

### 1. Sensitive Data Storage

```javascript
// services/storage/asyncStorage.js
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';

export const storageService = {
  // Non-sensitive data
  async setItem(key, value) {
    await AsyncStorage.setItem(key, JSON.stringify(value));
  },

  async getItem(key) {
    const item = await AsyncStorage.getItem(key);
    return item ? JSON.parse(item) : null;
  },

  // Sensitive data (tokens, passwords)
  async setSecureItem(key, value) {
    await SecureStore.setItemAsync(key, JSON.stringify(value));
  },

  async getSecureItem(key) {
    const item = await SecureStore.getItemAsync(key);
    return item ? JSON.parse(item) : null;
  },
};
```

### 2. API Security

```javascript
// services/api/interceptors.js
export const setupInterceptors = (instance) => {
  instance.interceptors.request.use(
    async (config) => {
      const token = await storageService.getSecureItem('authToken');
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
      return config;
    },
    (error) => Promise.reject(error)
  );

  instance.interceptors.response.use(
    (response) => response,
    async (error) => {
      if (error.response?.status === 401) {
        // Handle token refresh or logout
        dispatch(logout());
      }
      return Promise.reject(error);
    }
  );
};
```

### 3. Environment Variables

```javascript
// config/environment.js
import { API_URL, API_KEY } from '@env';

export const environment = {
  isDevelopment: __DEV__,
  isProduction: !__DEV__,
  apiUrl: API_URL,
  apiKey: API_KEY,
};
```

```
# .env file (NEVER commit)
API_URL=https://api.example.com
API_KEY=your-secret-key
```

---

## Development Workflow

### Git Workflow

```bash
# Feature branch naming
git checkout -b feature/user-authentication
git checkout -b bugfix/login-crash
git checkout -b chore/update-dependencies

# Commit message conventions
git commit -m "feat: implement user login flow"
git commit -m "fix: resolve null pointer exception"
git commit -m "docs: update README with setup instructions"
git commit -m "refactor: extract form validation logic"
git commit -m "test: add unit tests for auth service"
```

### Pre-commit Hooks

```json
// package.json
{
  "husky": {
    "hooks": {
      "pre-commit": "lint-staged",
      "pre-push": "npm test"
    }
  },
  "lint-staged": {
    "*.{js,jsx}": ["eslint --fix", "prettier --write"],
    "*.{json,md}": ["prettier --write"]
  }
}
```

### Build Configuration

```javascript
// app.json
{
  "expo": {
    "name": "MyApp",
    "slug": "myapp",
    "version": "1.0.0",
    "assetBundlePatterns": ["**/*"],
    "ios": {
      "supportsTabletMode": true,
      "bundleIdentifier": "com.mycompany.myapp"
    },
    "android": {
      "package": "com.mycompany.myapp",
      "versionCode": 1
    }
  }
}
```

---

## Documentation Standards

### Component Documentation

```javascript
/**
 * LoginForm Component
 * 
 * Renders a form for user authentication with email and password fields.
 * Includes validation, error handling, and loading states.
 * 
 * @component
 * @example
 * const handleLoginSuccess = (user) => {
 *   navigateTo('Home', { user });
 * };
 * 
 * <LoginForm onSuccess={handleLoginSuccess} />
 * 
 * @param {Object} props - Component props
 * @param {Function} props.onSuccess - Callback when login succeeds
 * @param {Function} [props.onError] - Callback when login fails
 * @returns {React.ReactElement} The login form component
 */
const LoginForm = ({ onSuccess, onError }) => {
  // Component implementation
};
```

### Function Documentation

```javascript
/**
 * Validates email format
 * 
 * @param {string} email - The email address to validate
 * @returns {boolean} True if email is valid, false otherwise
 * @throws {TypeError} If email is not a string
 * 
 * @example
 * isValidEmail('user@example.com'); // true
 * isValidEmail('invalid-email'); // false
 */
export const isValidEmail = (email) => {
  if (typeof email !== 'string') throw new TypeError('Email must be a string');
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
};
```

### README Documentation

```markdown
# My React Native App

## Overview
Brief description of the application

## Tech Stack
- React Native 0.72
- Redux Toolkit for state management
- React Navigation for routing
- Jest & React Testing Library

## Getting Started

### Prerequisites
- Node.js 16+
- npm or yarn
- Expo CLI

### Installation
\`\`\`bash
npm install
npm start
\`\`\`

### Environment Setup
1. Copy `.env.example` to `.env`
2. Update values with your API keys

## Project Structure
[Document folder structure and purpose]

## Development Guidelines
[Link to this guidelines document]

## Testing
\`\`\`bash
npm test
npm test -- --coverage
\`\`\`

## Deployment
[Instructions for iOS and Android builds]
```

---

## Best Practices Checklist

### Code Quality
- [ ] Code is DRY (Don't Repeat Yourself)
- [ ] Functions are pure when possible
- [ ] Components are atomic and reusable
- [ ] No console logs in production code
- [ ] Comments explain WHY, not WHAT
- [ ] Proper error boundaries implemented
- [ ] Loading states handled everywhere
- [ ] Empty states handled in lists

### Performance
- [ ] Components memoized appropriately
- [ ] FlatLists optimized with proper props
- [ ] Images optimized and lazy-loaded
- [ ] Unnecessary re-renders eliminated
- [ ] Bundle size monitored
- [ ] Network requests debounced/throttled

### Security
- [ ] Sensitive data in secure storage
- [ ] API keys not hardcoded
- [ ] Input validation on all forms
- [ ] Tokens properly refreshed
- [ ] No sensitive data in logs
- [ ] HTTPS enforced for API calls

### Testing
- [ ] Unit tests for utilities (>80% coverage)
- [ ] Component tests for UI logic
- [ ] Integration tests for flows
- [ ] E2E tests for critical paths
- [ ] Error scenarios tested

### Documentation
- [ ] README is current
- [ ] Components have JSDoc comments
- [ ] Complex logic documented
- [ ] Architecture decisions recorded
- [ ] API contracts documented
- [ ] Environment setup documented

---

## AI-Specific Coding Instructions

When generating code, AI should:

1. **Follow Project Structure**: Always place files in appropriate directories matching the structure above
2. **Use TypeScript**: Add type definitions for props, returns, and state
3. **Add Documentation**: Include JSDoc comments for all components and functions
4. **Implement Error Handling**: Every async operation should have try-catch and error states
5. **Create Tests**: Write unit tests alongside component implementation
6. **Follow Naming**: Use naming conventions consistently throughout
7. **Optimize Performance**: Use React.memo, useCallback, and useMemo appropriately
8. **Handle Loading States**: Always include loading and error states for async operations
9. **Validate Input**: Implement input validation for forms and API calls
10. **Use Redux Properly**: Update store through slices, use thunks for side effects
11. **Implement Logging**: Use logger utility for debugging and monitoring
12. **Security First**: Never hardcode secrets, use secure storage for sensitive data
13. **Keep Files Focused**: Single responsibility principle for each file
14. **Use Custom Hooks**: Extract reusable logic into custom hooks
15. **Test Coverage**: Aim for 80%+ test coverage on critical paths

---

## Quick Reference Commands

```bash
# Setup & Installation
npm install
npm start

# Development
npm run lint          # Run ESLint
npm run format        # Run Prettier
npm run test          # Run tests
npm run test:watch   # Watch mode
npm run test:coverage # Coverage report

# Build & Deploy
npm run build:android
npm run build:ios
npm run eject         # If using Expo

# Debugging
npm run log          # View device logs
npm run reverse      # Chrome DevTools
```

---

## Continuous Improvement

1. **Code Reviews**: All PRs must have peer review before merge
2. **Performance Monitoring**: Track metrics with Sentry or similar
3. **User Feedback**: Implement crash reporting and analytics
4. **Regular Audits**: Security audits quarterly
5. **Dependency Updates**: Keep libraries current with security patches
6. **Documentation Updates**: Keep docs in sync with code changes
7. **Technical Debt**: Dedicate sprint time for refactoring

---

**Last Updated**: February 2026
**Version**: 1.0.0

For questions or updates to these guidelines, please consult with the development team.