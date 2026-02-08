/**
 * Extract user-friendly error message from API response
 * @param {Object} response - API response object
 * @returns {string} - Formatted error message
 */
export const getErrorMessage = (response) => {
    // If there are specific validation errors, show them
    if (response?.errors && Array.isArray(response.errors) && response.errors.length > 0) {
        return response.errors.join(', ');
    }
    // Otherwise use the general message
    return response?.message || 'An error occurred';
};

/**
 * Handle API error and extract message
 * Handles both direct API responses and caught errors
 * @param {Error|Object} err - Error object or API response
 * @param {string} defaultMessage - Default message if no specific error found
 * @returns {string} - Formatted error message
 */
export const handleApiError = (err, defaultMessage = 'An error occurred') => {
    // If error has response data (from our api service)
    if (err.response?.data) {
        return getErrorMessage(err.response.data);
    }
    // If error has a message property
    if (err.message) {
        return err.message;
    }
    // Fallback to default
    return defaultMessage;
};
