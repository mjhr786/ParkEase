import { useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../../services/api';
import { handleApiError } from '../../utils/errorHandler';
import showToast from '../../utils/toast.jsx';

const STATUS_LABELS = [
  'Pending', 'Confirmed', 'In Progress', 'Completed', 'Cancelled',
  'Expired', 'Awaiting Payment', 'Rejected', 'Extension Pending', 'Extension Payment Due',
];

export default function AccessPassScanner() {
  const [token, setToken] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);

  const handleVerify = async (e) => {
    e.preventDefault();
    const value = token.trim();
    if (!value) {
      showToast.error('Paste or type an access-pass token');
      return;
    }

    setLoading(true);
    setResult(null);
    try {
      const response = await api.verifyAccessPass(value);
      if (response.success && response.data) {
        setResult(response.data);
        if (response.data.accessGranted) {
          showToast.success('Access granted');
        } else {
          showToast.error(response.data.denialMessage || 'Access denied');
        }
      } else {
        showToast.error(response.message || 'Verification failed');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to verify access pass'));
    }
    setLoading(false);
  };

  return (
    <div className="container" style={{ maxWidth: 640, paddingTop: '2rem', paddingBottom: '3rem' }}>
      <div className="flex-between" style={{ marginBottom: '1.25rem', alignItems: 'flex-start' }}>
        <div>
          <h1 className="page-title" style={{ marginBottom: 4 }}>Scan access pass</h1>
          <p className="card-subtitle" style={{ margin: 0 }}>
            Verify a guest QR / digital pass for your facilities (manual token entry for Phase 1).
          </p>
        </div>
        <Link to="/my/requests" className="btn btn-secondary">Back to inbox</Link>
      </div>

      <div className="card" style={{ marginBottom: '1.25rem' }}>
        <form onSubmit={handleVerify}>
          <div className="form-group">
            <label className="form-label">Access token</label>
            <textarea
              className="form-input"
              rows={3}
              placeholder="PE-BK… or paste from guest QR payload"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              style={{ fontFamily: 'ui-monospace, monospace', fontSize: '0.85rem' }}
            />
          </div>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Verifying…' : 'Verify pass'}
          </button>
        </form>
      </div>

      {result && (
        <div
          className="card"
          style={{
            borderColor: result.accessGranted ? 'rgba(16,185,129,0.45)' : 'rgba(239,68,68,0.45)',
            background: result.accessGranted ? 'rgba(16,185,129,0.08)' : 'rgba(239,68,68,0.08)',
          }}
        >
          <h3 className="card-title" style={{ color: result.accessGranted ? '#10b981' : '#f87171' }}>
            {result.accessGranted ? '✅ Access granted' : '⛔ Access denied'}
          </h3>
          {result.denialMessage && (
            <p style={{ color: '#fca5a5', marginTop: 0 }}>{result.denialMessage}</p>
          )}
          <dl style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '0.35rem 0.75rem', fontSize: '0.9rem', margin: 0 }}>
            <dt style={{ color: '#94a3b8' }}>Decision</dt>
            <dd style={{ margin: 0 }}>{result.decision}</dd>
            {result.denialReasonCode && (
              <>
                <dt style={{ color: '#94a3b8' }}>Reason</dt>
                <dd style={{ margin: 0 }}>{result.denialReasonCode}</dd>
              </>
            )}
            {result.bookingReference && (
              <>
                <dt style={{ color: '#94a3b8' }}>Reference</dt>
                <dd style={{ margin: 0 }}>{result.bookingReference}</dd>
              </>
            )}
            {result.parkingSpaceTitle && (
              <>
                <dt style={{ color: '#94a3b8' }}>Facility</dt>
                <dd style={{ margin: 0 }}>{result.parkingSpaceTitle}</dd>
              </>
            )}
            {result.status != null && (
              <>
                <dt style={{ color: '#94a3b8' }}>Status</dt>
                <dd style={{ margin: 0 }}>{STATUS_LABELS[result.status] ?? result.status}</dd>
              </>
            )}
            {result.vehicleNumber && (
              <>
                <dt style={{ color: '#94a3b8' }}>Plate</dt>
                <dd style={{ margin: 0 }}>{result.vehicleNumber}</dd>
              </>
            )}
            {result.startDateTime && (
              <>
                <dt style={{ color: '#94a3b8' }}>Window</dt>
                <dd style={{ margin: 0 }}>
                  {new Date(result.startDateTime).toLocaleString()} →{' '}
                  {result.endDateTime ? new Date(result.endDateTime).toLocaleString() : '—'}
                </dd>
              </>
            )}
          </dl>
        </div>
      )}
    </div>
  );
}
