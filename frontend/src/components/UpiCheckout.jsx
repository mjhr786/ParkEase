import { useEffect, useRef, useState } from 'react';
import api from '../services/api';

function qrUrl(intentUrl) {
    return `https://api.qrserver.com/v1/create-qr-code/?size=280x280&data=${encodeURIComponent(intentUrl)}`;
}

export default function UpiCheckout({ session, bookingId, onSuccess, onCancel }) {
    const [error, setError] = useState(null);
    const [copyState, setCopyState] = useState('Copy UPI URI');
    const [remaining, setRemaining] = useState('');
    const cancelled = useRef(false);

    useEffect(() => {
        cancelled.current = false;
        const tick = () => {
            const expires = new Date(session.expiresAtUtc).getTime();
            const ms = expires - Date.now();
            if (ms <= 0) {
                setRemaining('Expired');
                return;
            }
            const minutes = Math.floor(ms / 60000);
            const seconds = Math.floor((ms / 1000) % 60);
            setRemaining(`${minutes}m ${seconds}s`);
        };
        tick();
        const timer = setInterval(tick, 1000);
        return () => {
            cancelled.current = true;
            clearInterval(timer);
        };
    }, [session.expiresAtUtc]);

    useEffect(() => {
        cancelled.current = false;
        const poll = async () => {
            if (cancelled.current) return;
            if (new Date(session.expiresAtUtc).getTime() <= Date.now()) {
                setError('This UPI payment request expired. Start a new payment.');
                return;
            }
            try {
                const verifyRes = await api.verifyPayment({
                    bookingId,
                    razorpayPaymentId: session.providerPaymentId,
                    razorpayOrderId: session.providerOrderId,
                    rail: 'upi',
                });
                if (verifyRes.success) {
                    onSuccess(session.providerPaymentId);
                    return;
                }
            } catch {
                /* keep polling until expiry */
            }
        };

        poll();
        const id = setInterval(poll, 3000);
        return () => {
            cancelled.current = true;
            clearInterval(id);
        };
    }, [bookingId, session.providerPaymentId, session.providerOrderId, session.expiresAtUtc, onSuccess]);

    const copyUri = async () => {
        const uri = session.upiIntentUrl;
        try {
            await navigator.clipboard.writeText(uri);
            setCopyState('Copied full URI');
        } catch {
            setCopyState('Select the URI below');
        }
    };

    return (
        <div className="card" style={{ padding: '1rem', maxWidth: 360 }}>
            <h3 style={{ marginTop: 0 }}>Pay with UPI</h3>
            <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
                Scan this QR with any UPI app, or copy the full payment URI. Do not pay a VPA on its own.
            </p>
            <img
                src={qrUrl(session.upiIntentUrl)}
                alt="UPI payment QR"
                width={280}
                height={280}
                style={{ display: 'block', margin: '0 auto', background: '#fff' }}
            />
            <div style={{ marginTop: '0.75rem', fontSize: '0.85rem' }}>
                Time left: {remaining || '—'}
            </div>
            <button type="button" className="btn btn-secondary mt-1" onClick={copyUri}>
                {copyState}
            </button>
            <textarea
                readOnly
                value={session.upiIntentUrl}
                aria-label="Full UPI intent URI"
                style={{ width: '100%', marginTop: '0.5rem', minHeight: 72, fontSize: '0.75rem' }}
            />
            {error && <div className="alert alert-error mt-1">{error}</div>}
            <button type="button" className="btn btn-secondary mt-1" onClick={onCancel}>
                Cancel
            </button>
        </div>
    );
}
