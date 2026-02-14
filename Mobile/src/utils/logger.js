/**
 * Logger Utility
 * Structured logging with levels
 */

const LOG_LEVELS = {
    DEBUG: 0,
    INFO: 1,
    WARN: 2,
    ERROR: 3,
};

class Logger {
    constructor(logLevel = __DEV__ ? LOG_LEVELS.DEBUG : LOG_LEVELS.ERROR) {
        this.logLevel = logLevel;
    }

    debug(tag, message, data = null) {
        if (this.logLevel <= LOG_LEVELS.DEBUG) {
            console.log(`[DEBUG] ${tag}: ${message}`, data || '');
        }
    }

    info(tag, message, data = null) {
        if (this.logLevel <= LOG_LEVELS.INFO) {
            console.log(`[INFO] ${tag}: ${message}`, data || '');
        }
    }

    warn(tag, message, data = null) {
        if (this.logLevel <= LOG_LEVELS.WARN) {
            console.warn(`[WARN] ${tag}: ${message}`, data || '');
        }
    }

    error(tag, message, error = null) {
        if (this.logLevel <= LOG_LEVELS.ERROR) {
            console.error(`[ERROR] ${tag}: ${message}`, error || '');
        }
    }
}

export default new Logger();
