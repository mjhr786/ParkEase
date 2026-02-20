/**
 * Validators
 * Form input validation utilities
 */

export const isValidEmail = (email) => {
    if (typeof email !== 'string') return false;
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
};

export const isValidPassword = (password) => {
    return typeof password === 'string' && password.length >= 8;
};

export const isValidPhone = (phone) => {
    if (typeof phone !== 'string') return false;
    return /^[\d\s\-+()]{7,15}$/.test(phone.trim());
};

export const isRequired = (value) => {
    if (typeof value === 'string') return value.trim().length > 0;
    return value != null;
};

export const isMinLength = (value, min) => {
    return typeof value === 'string' && value.trim().length >= min;
};

/**
 * Validate a form data object against a rules object
 * @param {Object} data - Form data
 * @param {Object} rules - Validation rules { field: [{ validator, message }] }
 * @returns {{ isValid: boolean, errors: Object }}
 */
export const validateForm = (data, rules) => {
    const errors = {};
    let isValid = true;

    Object.entries(rules).forEach(([field, fieldRules]) => {
        for (const rule of fieldRules) {
            if (!rule.validator(data[field])) {
                errors[field] = rule.message;
                isValid = false;
                break;
            }
        }
    });

    return { isValid, errors };
};

export const loginRules = {
    email: [
        { validator: isRequired, message: 'Email is required' },
        { validator: isValidEmail, message: 'Please enter a valid email' },
    ],
    password: [
        { validator: isRequired, message: 'Password is required' },
    ],
};

export const registerRules = {
    firstName: [
        { validator: isRequired, message: 'First name is required' },
    ],
    lastName: [
        { validator: isRequired, message: 'Last name is required' },
    ],
    email: [
        { validator: isRequired, message: 'Email is required' },
        { validator: isValidEmail, message: 'Please enter a valid email' },
    ],
    password: [
        { validator: isRequired, message: 'Password is required' },
        { validator: isValidPassword, message: 'Password must be at least 8 characters' },
    ],
    phoneNumber: [
        { validator: isRequired, message: 'Phone number is required' },
        { validator: isValidPhone, message: 'Please enter a valid phone number' },
    ],
};
