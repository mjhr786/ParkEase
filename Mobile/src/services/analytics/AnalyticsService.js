import analytics from '@react-native-firebase/analytics';
import crashlytics from '@react-native-firebase/crashlytics';
import Constants from 'expo-constants';
import * as Crypto from 'expo-crypto';
import { Platform } from 'react-native';

const MAX_VALUE_LENGTH = 100;
const MAX_PARAM_LENGTH = 40;

const trimValue = (value, maxLength = MAX_VALUE_LENGTH) => {
    if (value === undefined || value === null) {
        return undefined;
    }

    const normalized = String(value).trim();
    if (!normalized) {
        return undefined;
    }

    return normalized.slice(0, maxLength);
};

const sanitizeKey = (key) =>
    String(key)
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9_]/g, '_')
        .slice(0, MAX_PARAM_LENGTH);

const compactObject = (input) =>
    Object.fromEntries(
        Object.entries(input).filter(([, value]) => value !== undefined && value !== null && value !== '')
    );

const normalizeIdentifier = async (value) => {
    const normalized = trimValue(value, 256);
    if (!normalized) {
        return undefined;
    }

    return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, normalized.toLowerCase());
};

const getEmailDomain = (email) => {
    const normalized = trimValue(email, 256);
    if (!normalized || !normalized.includes('@')) {
        return undefined;
    }

    return trimValue(normalized.split('@')[1]);
};

const getDeviceContext = () => {
    const expoConfig = Constants.expoConfig || Constants.manifest || {};
    const platformConfig = expoConfig[Platform.OS] || {};
    const platformConstants = Platform.constants || {};

    return compactObject({
        platform: trimValue(Platform.OS),
        app_version: trimValue(
            Constants.expoConfig?.version ||
            Constants.manifest?.version ||
            expoConfig.version
        ),
        app_build: trimValue(
            platformConfig.buildNumber ||
            platformConfig.versionCode ||
            Constants.nativeBuildVersion
        ),
        device_os: trimValue(Platform.OS),
        os_version: trimValue(Platform.Version),
        device_brand: trimValue(platformConstants.Brand || platformConstants.brand),
        device_manufacturer: trimValue(platformConstants.Manufacturer || platformConstants.manufacturer),
        device_model: trimValue(platformConstants.Model || platformConstants.model),
        app_ownership: trimValue(Constants.appOwnership),
        execution_env: trimValue(Constants.executionEnvironment),
    });
};

class AnalyticsService {
    constructor() {
        this.initialized = false;
        this.globalErrorHandlerInstalled = false;
        this.previousErrorHandler = null;
    }

    async initialize() {
        if (this.initialized) {
            return;
        }

        await analytics().setAnalyticsCollectionEnabled(true);
        await crashlytics().setCrashlyticsCollectionEnabled(true);
        await this.syncDeviceContext();
        this.installGlobalErrorHandler();
        this.initialized = true;
    }

    async syncDeviceContext(extraContext = {}) {
        const context = compactObject({
            ...getDeviceContext(),
            ...extraContext,
        });

        await analytics().setUserProperties(context);
        await crashlytics().setAttributes(context);
    }

    async syncUserContext(user) {
        await this.initialize();

        const deviceContext = getDeviceContext();

        if (!user) {
            await analytics().setUserId(null);
            await analytics().setUserProperties({
                ...deviceContext,
                auth_state: 'signed_out',
                user_role: undefined,
                email_domain: undefined,
                user_id_hash: undefined,
                email_hash: undefined,
                phone_hash: undefined,
            });
            await crashlytics().setUserId('');
            await crashlytics().setAttributes({
                ...deviceContext,
                auth_state: 'signed_out',
            });
            return;
        }

        const userId = trimValue(user.id || user.userId || user._id, 64);
        const [emailHash, phoneHash, userIdHash] = await Promise.all([
            normalizeIdentifier(user.email),
            normalizeIdentifier(user.phoneNumber || user.phone),
            normalizeIdentifier(userId),
        ]);

        const userContext = compactObject({
            auth_state: 'signed_in',
            user_role: trimValue(user.role || user.userRole),
            email_domain: getEmailDomain(user.email),
            user_id_hash: trimValue(userIdHash, 64),
            email_hash: trimValue(emailHash, 64),
            phone_hash: trimValue(phoneHash, 64),
            first_name: trimValue(user.firstName),
            last_name: trimValue(user.lastName),
            city: trimValue(user.city),
            state: trimValue(user.state),
            country: trimValue(user.country),
            account_status: trimValue(user.status || user.accountStatus),
            auth_provider: trimValue(user.provider || user.authProvider),
        });

        if (userId) {
            await analytics().setUserId(userId);
            await crashlytics().setUserId(userId);
        }

        await analytics().setUserProperties({
            ...deviceContext,
            ...userContext,
        });
        await crashlytics().setAttributes({
            ...deviceContext,
            ...userContext,
            user_email_domain: userContext.email_domain,
        });
    }

    async trackError(tag, message, error = null, context = {}) {
        await this.initialize();

        const statusCode = error?.response?.status || error?.statusCode;
        const code = error?.code || error?.response?.data?.code;
        const eventParams = compactObject({
            error_tag: trimValue(tag, MAX_PARAM_LENGTH),
            error_message: trimValue(message, MAX_VALUE_LENGTH),
            error_code: trimValue(code, MAX_PARAM_LENGTH),
            error_status: statusCode ? Number(statusCode) : undefined,
            is_fatal: context.isFatal ? 1 : 0,
        });

        try {
            await analytics().logEvent('app_error_logged', eventParams);
        } catch (_) {
            // Avoid recursive failures while logging analytics events.
        }

        const normalizedError = error instanceof Error
            ? error
            : new Error(trimValue(message, 500) || 'Unknown app error');

        const attributes = compactObject({
            error_tag: trimValue(tag, MAX_PARAM_LENGTH),
            error_code: trimValue(code, MAX_PARAM_LENGTH),
            error_status: trimValue(statusCode, MAX_PARAM_LENGTH),
            error_message: trimValue(message, MAX_VALUE_LENGTH),
            ...Object.fromEntries(
                Object.entries(context).map(([key, value]) => [sanitizeKey(key), trimValue(value, MAX_VALUE_LENGTH)])
            ),
        });

        try {
            await crashlytics().setAttributes(attributes);
            crashlytics().log(`[${tag}] ${message}`);
            crashlytics().recordError(normalizedError);
        } catch (_) {
            // Best-effort tracking only.
        }
    }

    installGlobalErrorHandler() {
        if (this.globalErrorHandlerInstalled || !global.ErrorUtils?.setGlobalHandler) {
            return;
        }

        this.previousErrorHandler = global.ErrorUtils.getGlobalHandler?.() || null;

        global.ErrorUtils.setGlobalHandler((error, isFatal) => {
            this.trackError(
                'GlobalError',
                error?.message || 'Unhandled JavaScript error',
                error,
                { isFatal: Boolean(isFatal) }
            );

            if (this.previousErrorHandler) {
                this.previousErrorHandler(error, isFatal);
            }
        });

        this.globalErrorHandlerInstalled = true;
    }
}

export default new AnalyticsService();
