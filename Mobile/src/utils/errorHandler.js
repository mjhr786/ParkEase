/**
 * Error Handler
 * Centralized error processing
 */

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
        const data = error.response.data;
        return {
            message: data?.message || data?.title || 'Server error occurred',
            code: data?.code || 'SERVER_ERROR',
            statusCode: error.response.status,
            errors: data?.errors || [],
        };
    }

    if (error.request) {
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

/**
 * Extract user-friendly error message from API error
 * @param {*} error
 * @returns {string}
 */
export const getErrorMessage = (error) => {
    if (typeof error === 'string') return error;
    const handled = handleError(error);
    return handled.message;
};

export default { AppError, handleError, getErrorMessage };
