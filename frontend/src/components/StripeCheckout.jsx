import { useMemo, useState } from 'react';
import { loadStripe } from '@stripe/stripe-js';
import { Elements, PaymentElement, useStripe, useElements } from '@stripe/react-stripe-js';
import { useTheme } from '../contexts/ThemeContext';

let stripePromise = null;

function getStripePromise(publishableKey) {
    if (!stripePromise) {
        stripePromise = loadStripe(publishableKey);
    }
    return stripePromise;
}

function CheckoutForm({ onSuccess, onCancel, bookingId }) {
    const stripe = useStripe();
    const elements = useElements();
    const [processing, setProcessing] = useState(false);
    const [error, setError] = useState(null);

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (!stripe || !elements) return;

        setProcessing(true);
        setError(null);

        const { error: submitError } = await elements.submit();
        if (submitError) {
            setError(submitError.message);
            setProcessing(false);
            return;
        }

        const { error: confirmError, paymentIntent } = await stripe.confirmPayment({
            elements,
            confirmParams: {
                return_url: window.location.href,
            },
            redirect: 'if_required',
        });

        if (confirmError) {
            setError(confirmError.message);
            setProcessing(false);
        } else if (paymentIntent && paymentIntent.status === 'succeeded') {
            onSuccess(paymentIntent.id);
        } else {
            setError('Payment was not completed. Please try again.');
            setProcessing(false);
        }
    };

    return (
        <form onSubmit={handleSubmit}>
            <PaymentElement />
            {error && (
                <div className="alert alert-error mt-1" style={{ fontSize: '0.9rem' }}>
                    {error}
                </div>
            )}
            <div className="flex gap-1 mt-2">
                <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={!stripe || processing}
                >
                    {processing ? 'Processing...' : 'Pay Now'}
                </button>
                <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={onCancel}
                    disabled={processing}
                >
                    Cancel
                </button>
            </div>
        </form>
    );
}

function readCssVar(name, fallback) {
    if (typeof window === 'undefined') return fallback;
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return value || fallback;
}

export default function StripeCheckout({ clientSecret, publishableKey, onSuccess, onCancel, bookingId }) {
    const { theme, isLight } = useTheme();

    const options = useMemo(() => {
        if (!clientSecret) return null;
        return {
            clientSecret,
            appearance: {
                theme: isLight ? 'stripe' : 'night',
                variables: {
                    colorPrimary: readCssVar('--color-primary', isLight ? '#4f46e5' : '#6366f1'),
                    colorBackground: readCssVar('--color-bg-tertiary', isLight ? '#f1f5f9' : '#1a1a25'),
                    colorText: readCssVar('--color-text-primary', isLight ? '#0f172a' : '#ffffff'),
                    colorDanger: readCssVar('--color-error', '#ef4444'),
                    borderRadius: '8px',
                    fontFamily: 'Inter, sans-serif',
                },
            },
        };
    }, [clientSecret, theme, isLight]);

    if (!clientSecret || !publishableKey || !options) return null;

    return (
        <div className="stripe-checkout">
            {/* Remount Elements when theme changes so Stripe re-applies appearance */}
            <Elements
                key={theme}
                stripe={getStripePromise(publishableKey)}
                options={options}
            >
                <CheckoutForm onSuccess={onSuccess} onCancel={onCancel} bookingId={bookingId} />
            </Elements>
        </div>
    );
}
