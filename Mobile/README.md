# ParkEase Mobile Application

Welcome to the **ParkEase Mobile** repository. This is an Expo-managed React Native application providing a seamless interface for users to find, book, and manage parking spaces, while allowing hosts to list their properties.

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Installation & Setup](#installation--setup)
3. [Running the App Locally](#running-the-app-locally)
4. [Project Architecture & Guidelines](#project-architecture--guidelines)
5. [Global Error Handling & Debugging](#global-error-handling--debugging)
6. [Generating an APK (Android)](#generating-an-apk-android)
7. [Deploying for Production](#deploying-for-production)

---

## Prerequisites

Before starting, ensure you have the following installed on your machine:
- **Node.js** (v18 or higher recommended)
- **npm** or **yarn**
- **Expo CLI**: `npm install -g expo-cli`
- **EAS CLI** (Expo Application Services for building/deploying): `npm install -g eas-cli`
- **Xcode** (for Mac users wanting to simulate iOS)
- **Android Studio** (for Simulating Android devices locally)

---

## Installation & Setup

1. **Clone the repository** and navigate to the mobile directory:
   ```bash
   cd ParkEase/Mobile
   ```
2. **Install dependencies**:
   ```bash
   npm install
   ```
3. **Environment Setup**: Ensure your backend API is running and that your local environment variables mapping to `src/config/environment.js` point to your local machine IP or hosted domain (e.g. `http://YOUR_LOCAL_IP:5000/api`). Avoid using `localhost` if running on physical devices.

---

## Running the App Locally

Expo provides an interactive terminal UI. Start the Metro bundler using:
```bash
npm start
```
From the interactive terminal menu that launches, you can press:
- `i` — To open the app on the **iOS Simulator** (requires Xcode). Alternatively, directly run `npx expo start --ios`.
- `a` — To open the app on the **Android Emulator** (requires Android Studio). Alternatively, directly run `npx expo start --android`.
- `Scan QR Code` — Download the **Expo Go** app on your physical iPhone/Android device and scan the terminal QR code to run it live.

---

## Project Architecture & Guidelines

We strictly follow the architectural conventions defined in **`AppGuidelines.md`** located at the root of the project.

- **State Management**: Redux Toolkit `slice` & `thunk` architecture inside `src/store/`.
- **Navigation**: React Navigation stacked tabs (`AuthNavigator`, `AppTabNavigator`).
- **Styles**: We use a centralized style dictionary (`src/styles/globalStyles.js`) instead of scattered inline styles.
- **Component Design**: Every module exports typed/commented standard React Functional Components using standard clean coding principles.

---

## Global Error Handling & Debugging

The app employs a unified error masking logic. 
In development environments, explicit `console.error` calls will normally trigger intrusive grey **LogBox** popups on the device screen. To circumvent this and ensure proper UI continuity:

1. **Top-Edge Automatic Error Banner**: An `EventBus` module explicitly listens for unhandled system exceptions or `4xx`/`5xx` REST failures.
2. **Implementation**: When `apiClient.js` experiences a backend crash or timeout, it fires:
   ```javascript
   EventBus.emit('SHOW_ERROR_BANNER', { title: 'Network Issue', message: 'Failed to connect' });
   ```
3. **Logging Silently**: Always use the native `logger.js` utility (`import logger from '../../utils/logger'`) for internal debugging using `logger.debug()` or `console.log()` to prevent the Expo LogBox UI from hijacking the screen during development.
4. **Token Expiration**: The Axios client seamlessly intercepts `401 Unauthorized` responses and pauses pending network requests to query `/auth/refresh` silently via secure storage before unfreezing queues.

---

## Generating an APK (Android Testing)

To generate a standalone `.apk` Android installation file locally from the native Android project:

1. **Install Android prerequisites**:
   ```bash
   Android Studio
   Java 17
   ```
2. **Build the release APK locally**:
   ```bash
   npm run android:build:apk
   ```
3. **Find the generated APK**:
   ```bash
   android/app/build/outputs/apk/release/app-release.apk
   ```
4. **Build an AAB instead when needed**:
   ```bash
   npm run android:build:aab
   ```
5. **Find the generated AAB**:
   ```bash
   android/app/build/outputs/bundle/release/app-release.aab
   ```

If `android/keystore.properties` is present, release builds use your release keystore. Otherwise they fall back to the debug keystore for local testing.

## Firebase App Distribution

This project can upload locally built Android releases directly to Firebase App Distribution through Gradle.

1. **Create local config files**:
   ```bash
   cp android/firebase-app-distribution.properties.example android/firebase-app-distribution.properties
   cp android/keystore.properties.example android/keystore.properties
   ```
2. **Fill in Firebase distribution values** in `android/firebase-app-distribution.properties`:
   - `FIREBASE_GROUPS` or `FIREBASE_TESTERS`
   - `FIREBASE_RELEASE_NOTES` or `FIREBASE_RELEASE_NOTES_FILE`
   - `FIREBASE_CREDENTIALS_FILE` if you want Gradle to use a service account JSON directly
3. **Authenticate with Firebase** using one of these options:
   - Set `GOOGLE_APPLICATION_CREDENTIALS` to a Firebase service account JSON with App Distribution Admin access
   - Or set `FIREBASE_TOKEN` after logging in with the Firebase CLI
4. **Upload an APK to Firebase App Distribution**:
   ```bash
   npm run android:distribute:apk
   ```
5. **Upload an AAB to Firebase App Distribution**:
   ```bash
   npm run android:distribute:aab
   ```

The Gradle upload task is `appDistributionUploadRelease`. It builds locally on your machine first, then uploads the generated artifact to Firebase. Firebase stores the tester distribution; it does not compile the Android app for you.

## Slack Build Notifications

You can post each successful Firebase App Distribution release to Slack after upload.

1. **Create a local `.env` file in the project root**:
   ```bash
   cat <<'EOF' > .env
   SLACK_WEBHOOK_URL="your-incoming-webhook-url"
   SLACK_CHANNEL="#qa-builds-android"
   EOF
   ```
2. **Confirm Firebase local config exists**:
   - `android/firebase-app-distribution.properties`
   - `android/keystore.properties` if you want release signing instead of debug signing fallback
3. **Run the combined Firebase + Slack flow for APK**:
   ```bash
   npm run android:distribute:apk:slack
   ```
4. **Run the combined Firebase + Slack flow for AAB**:
   ```bash
   npm run android:distribute:aab:slack
   ```
5. **What the script does**:
   - increments Android `versionCode` in `android/app/build.gradle` before the release starts
   - generates release notes from git commits and local file changes
   - builds the Android artifact locally with Gradle
   - uploads the build to Firebase App Distribution
   - reads the Firebase release links from Gradle output
   - posts those links to Slack using the webhook from `.env`
6. **What success looks like**:
   - the terminal prints the new Android build number
   - the terminal prints the generated release notes summary
   - Firebase prints a console URL, tester URL, and temporary binary download URL
   - Slack receives a message with the Firebase tester link and console link

The Slack release helper script is [distributeAndroidToFirebaseAndSlack.js](/Users/mohammad.shaikh/Documents/TOOL/agent/AG/Park/ParkEase/Mobile/scripts/distributeAndroidToFirebaseAndSlack.js). It automatically loads `.env` from the project root, so you do not need to export the Slack variables manually in your shell.

The Slack message includes the generated release notes summary, Firebase tester link, Firebase console link, and the temporary direct binary download link when Firebase returns it.

Important: Slack incoming webhooks post to the channel selected when the webhook was created. The `SLACK_CHANNEL` variable is included in the message for visibility, but incoming webhooks do not override the webhook's configured destination channel.

If the Slack webhook is valid but the message does not appear in the channel you expect, check the webhook configuration inside Slack first. The webhook destination channel is controlled by Slack, not by the `SLACK_CHANNEL` value in `.env`.

---

## Deploying for Production

When you are ready to distribute to the **Apple App Store** (.ipa) or **Google Play Store** (.aab Android App Bundle):

1. Double-check your app versions and credentials in `app.json`.
2. Run the production build via EAS:
   ```bash
   eas build --platform all --profile production
   ```
3. Use `eas submit` to automatically push the compiled binaries directly to App Store Connect or Google Play Console!
