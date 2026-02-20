/**
 * Formatters
 * Currency, date, and display formatting utilities
 */

/**
 * Format currency amount
 * @param {number} amount
 * @param {string} currency - Currency code (default: INR)
 * @returns {string}
 */
export const formatCurrency = (amount, currency = 'INR') => {
    if (amount == null || isNaN(amount)) return '₹0';
    return new Intl.NumberFormat('en-IN', {
        style: 'currency',
        currency,
        minimumFractionDigits: 0,
        maximumFractionDigits: 2,
    }).format(amount);
};

/**
 * Format date to readable string
 * @param {string|Date} date
 * @param {Object} options
 * @returns {string}
 */
export const formatDate = (date, options = {}) => {
    if (!date) return '';
    const d = new Date(date);
    const defaults = {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
        ...options,
    };
    return d.toLocaleDateString('en-IN', defaults);
};

/**
 * Format time
 * @param {string|Date} date
 * @returns {string}
 */
export const formatTime = (date) => {
    if (!date) return '';
    const d = new Date(date);
    return d.toLocaleTimeString('en-IN', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: true,
    });
};

/**
 * Format date and time together
 * @param {string|Date} date
 * @returns {string}
 */
export const formatDateTime = (date) => {
    if (!date) return '';
    return `${formatDate(date)} ${formatTime(date)}`;
};

/**
 * Format relative time (e.g., "2 hours ago")
 * @param {string|Date} date
 * @returns {string}
 */
export const formatRelativeTime = (date) => {
    if (!date) return '';
    const now = new Date();
    const d = new Date(date);
    const diff = now - d;
    const seconds = Math.floor(diff / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (seconds < 60) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    if (days < 7) return `${days}d ago`;
    return formatDate(date);
};

/**
 * Format address from parking space data
 * @param {Object} parking
 * @returns {string}
 */
export const formatAddress = (parking) => {
    if (!parking) return '';
    const parts = [parking.address, parking.city, parking.state].filter(Boolean);
    return parts.join(', ');
};

/**
 * Truncate text with ellipsis
 * @param {string} text
 * @param {number} maxLength
 * @returns {string}
 */
export const truncateText = (text, maxLength = 100) => {
    if (!text || text.length <= maxLength) return text || '';
    return text.substring(0, maxLength).trim() + '...';
};

/**
 * Format rating as stars display
 * @param {number} rating
 * @returns {string}
 */
export const formatRating = (rating) => {
    if (rating == null) return '0.0';
    return Number(rating).toFixed(1);
};
