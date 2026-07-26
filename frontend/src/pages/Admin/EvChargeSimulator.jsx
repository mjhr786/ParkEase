import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';

/**
 * Demo: start → meter → stop EV charge for a booking (mock OCPP station).
 * Use with Per-kWh EV listings to settle energy fee on the booking.
 */
export default function EvChargeSimulator() {
  const { isAuthenticated, isAdmin, loading: authLoading } = useAuth();
  const navigate = useNavigate();

  const [bookingId, setBookingId] = useState('');
  const [energyKwh, setEnergyKwh] = useState('12.5');
  const [stationId, setStationId] = useState('MOCK-1');
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);

  if (!authLoading && !isAuthenticated) {
    navigate('/login');
    return null;
  }

  const onSubmit = async (e) => {
    e.preventDefault();
    if (!bookingId.trim()) {
      toast.error('Booking id is required');
      return;
    }
    const kwh = parseFloat(energyKwh);
    if (!(kwh > 0)) {
      toast.error('Energy kWh must be positive');
      return;
    }

    setBusy(true);
    setResult(null);
    try {
      const res = await api.simulateEvChargingSession({
        bookingId: bookingId.trim(),
        energyKwh: kwh,
        stationId: stationId.trim() || 'MOCK-1',
      });
      if (!res) return;
      setResult(res.data ?? res);
      if (res.success) {
        toast.success(res.message || 'Charge session completed');
      } else {
        toast.error(res.message || 'Simulator failed');
      }
    } catch (err) {
      toast.error(err.message || 'Simulator request failed');
    } finally {
      setBusy(false);
    }
  };

  const card = {
    background: 'var(--color-surface)',
    borderRadius: '12px',
    padding: '1.25rem 1.5rem',
    border: '1px solid var(--color-border)',
  };

  return (
    <div className="container" style={{ maxWidth: 720, padding: '2rem 1rem 4rem' }}>
      <h1 style={{ marginBottom: '0.35rem' }}>EV Charge Simulator</h1>
      <p style={{ color: 'var(--color-text-muted)', marginBottom: '1.5rem' }}>
        Mock OCPP pipeline: start transaction → meter values → stop and settle energy fee (Per kWh mode).
        {isAdmin ? ' Admin can simulate any booking.' : ' Vendors can simulate for their facilities; guests for their own bookings.'}
      </p>

      <form onSubmit={onSubmit} style={card}>
        <div className="form-group">
          <label className="form-label">Booking ID</label>
          <input
            className="form-input"
            value={bookingId}
            onChange={(e) => setBookingId(e.target.value)}
            placeholder="Guid from My Bookings"
            required
          />
        </div>
        <div className="grid grid-2" style={{ gap: '0.75rem' }}>
          <div className="form-group">
            <label className="form-label">Energy delivered (kWh)</label>
            <input
              type="number"
              className="form-input"
              min="0.001"
              step="0.1"
              value={energyKwh}
              onChange={(e) => setEnergyKwh(e.target.value)}
              required
            />
          </div>
          <div className="form-group">
            <label className="form-label">Station ID</label>
            <input
              className="form-input"
              value={stationId}
              onChange={(e) => setStationId(e.target.value)}
            />
          </div>
        </div>
        <button type="submit" className="btn btn-primary" disabled={busy} style={{ marginTop: '0.5rem' }}>
          {busy ? 'Running…' : 'Simulate full charge session'}
        </button>
      </form>

      {result && (
        <div style={{ ...card, marginTop: '1.25rem' }}>
          <h3 style={{ marginTop: 0 }}>Result</h3>
          <dl style={{ display: 'grid', gridTemplateColumns: '140px 1fr', gap: '0.35rem 0.75rem', margin: 0 }}>
            <dt style={{ color: 'var(--color-text-secondary)' }}>Status</dt>
            <dd style={{ margin: 0 }}>{result.status}</dd>
            <dt style={{ color: 'var(--color-text-secondary)' }}>Transaction</dt>
            <dd style={{ margin: 0, wordBreak: 'break-all' }}>{result.ocppTransactionId}</dd>
            <dt style={{ color: 'var(--color-text-secondary)' }}>Energy</dt>
            <dd style={{ margin: 0 }}>{result.energyDeliveredKwh} kWh</dd>
            <dt style={{ color: 'var(--color-text-secondary)' }}>Rate</dt>
            <dd style={{ margin: 0 }}>₹{result.ratePerKwh}/kWh</dd>
            <dt style={{ color: 'var(--color-text-secondary)' }}>Energy fee</dt>
            <dd style={{ margin: 0 }}>₹{result.energyFeeAmount}</dd>
          </dl>
        </div>
      )}
    </div>
  );
}
