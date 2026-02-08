import toast from 'react-hot-toast';

// Custom toast wrapper that shows dismissible toasts with close button
const toastStyles = {
    container: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        width: '100%',
    },
    message: {
        flex: 1,
    },
    closeBtn: {
        background: 'transparent',
        border: 'none',
        color: '#94a3b8',
        cursor: 'pointer',
        fontSize: '20px',
        padding: '0 4px',
        lineHeight: 1,
        marginLeft: '8px',
    },
};

const createToast = (message, type = 'default') => {
    const toastFn = type === 'success' ? toast.success : type === 'error' ? toast.error : toast;

    return toastFn(
        (t) => (
            <div style={toastStyles.container}>
                <span style={toastStyles.message}>{message}</span>
                <button
                    onClick={() => toast.dismiss(t.id)}
                    style={toastStyles.closeBtn}
                    aria-label="Close notification"
                >
                    ×
                </button>
            </div>
        ),
        { id: `toast-${Date.now()}` }
    );
};

export const showToast = {
    success: (message) => createToast(message, 'success'),
    error: (message) => createToast(message, 'error'),
    info: (message) => createToast(message, 'default'),
};

export default showToast;
