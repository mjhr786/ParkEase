import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';

const RULE_TYPES = [
  { value: 1, label: 'Allow' },
  { value: 2, label: 'Deny' },
];

export default function LprRegistry() {
  const { parkingSpaceId } = useParams();
  const { isAuthenticated, loading: authLoading } = useAuth();
  const navigate = useNavigate();

  const [keys, setKeys] = useState([]);
  const [rules, setRules] = useState([]);
  const [loading, setLoading] = useState(true);
  const [keyName, setKeyName] = useState('');
  const [keyId, setKeyId] = useState('');
  const [createdSecret, setCreatedSecret] = useState(null);
  const [plate, setPlate] = useState('');
  const [ruleType, setRuleType] = useState(2);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);

  if (!authLoading && !isAuthenticated) {
    navigate('/login');
  }

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [keysRes, rulesRes] = await Promise.all([
        api.getLprCameraKeys(parkingSpaceId),
        api.getLprPlateRules(parkingSpaceId),
      ]);
      if (keysRes?.success) setKeys(keysRes.data || []);
      else if (keysRes && !keysRes.success) toast.error(keysRes.message || 'Failed to load camera keys');
      if (rulesRes?.success) setRules(rulesRes.data || []);
      else if (rulesRes && !rulesRes.success) toast.error(rulesRes.message || 'Failed to load plate rules');
    } catch (e) {
      toast.error(e.message || 'Failed to load LPR registry');
    } finally {
      setLoading(false);
    }
  }, [parkingSpaceId]);

  useEffect(() => {
    if (isAuthenticated && parkingSpaceId) load();
  }, [isAuthenticated, parkingSpaceId, load]);

  const createKey = async (e) => {
    e.preventDefault();
    if (!keyName.trim()) return toast.error('Name is required');
    setBusy(true);
    setCreatedSecret(null);
    try {
      const res = await api.createLprCameraKey(parkingSpaceId, {
        name: keyName.trim(),
        keyId: keyId.trim() || null,
      });
      if (res?.success) {
        toast.success(res.message || 'Camera key created');
        setCreatedSecret(res.data?.secret || null);
        setKeyName('');
        setKeyId('');
        await load();
      } else {
        toast.error(res?.message || 'Create failed');
      }
    } catch (err) {
      toast.error(err.message || 'Create failed');
    } finally {
      setBusy(false);
    }
  };

  const toggleKey = async (id, isEnabled) => {
    setBusy(true);
    try {
      const res = await api.setLprCameraKeyEnabled(parkingSpaceId, id, !isEnabled);
      if (res?.success) {
        toast.success(res.message || 'Updated');
        await load();
      } else toast.error(res?.message || 'Update failed');
    } finally {
      setBusy(false);
    }
  };

  const deleteKey = async (id) => {
    if (!window.confirm('Delete this camera key? Cameras using it will stop working.')) return;
    setBusy(true);
    try {
      const res = await api.deleteLprCameraKey(parkingSpaceId, id);
      if (res?.success) {
        toast.success('Deleted');
        await load();
      } else toast.error(res?.message || 'Delete failed');
    } finally {
      setBusy(false);
    }
  };

  const createRule = async (e) => {
    e.preventDefault();
    if (!plate.trim()) return toast.error('License plate is required');
    setBusy(true);
    try {
      const res = await api.createLprPlateRule(parkingSpaceId, {
        licensePlate: plate.trim(),
        ruleType: Number(ruleType),
        note: note.trim() || null,
      });
      if (res?.success) {
        toast.success(res.message || 'Rule created');
        setPlate('');
        setNote('');
        await load();
      } else toast.error(res?.message || 'Create failed');
    } catch (err) {
      toast.error(err.message || 'Create failed');
    } finally {
      setBusy(false);
    }
  };

  const toggleRule = async (id, isEnabled) => {
    setBusy(true);
    try {
      const res = await api.setLprPlateRuleEnabled(parkingSpaceId, id, !isEnabled);
      if (res?.success) {
        await load();
      } else toast.error(res?.message || 'Update failed');
    } finally {
      setBusy(false);
    }
  };

  const deleteRule = async (id) => {
    if (!window.confirm('Delete this plate rule?')) return;
    setBusy(true);
    try {
      const res = await api.deleteLprPlateRule(parkingSpaceId, id);
      if (res?.success) {
        toast.success('Deleted');
        await load();
      } else toast.error(res?.message || 'Delete failed');
    } finally {
      setBusy(false);
    }
  };

  const card = {
    background: '#1e293b',
    borderRadius: 12,
    padding: '1.25rem 1.5rem',
    border: '1px solid rgba(255,255,255,0.06)',
    marginBottom: '1.25rem',
  };

  return (
    <div className="container" style={{ maxWidth: 900, padding: '2rem 1rem 4rem' }}>
      <div style={{ marginBottom: '0.5rem' }}>
        <Link to="/my/listings" style={{ color: '#94a3b8', fontSize: '0.9rem' }}>← My Listings</Link>
      </div>
      <h1 style={{ marginBottom: 4 }}>LPR facility registry</h1>
      <p style={{ color: 'var(--color-text-muted)', marginBottom: '1.5rem', fontSize: '0.95rem' }}>
        Manage camera API keys and plate allow/deny lists for this parking space.
        Copy secrets immediately after creation — they are not shown again.
      </p>
      <p style={{ color: '#64748b', fontSize: '0.8rem', marginBottom: '1.5rem', wordBreak: 'break-all' }}>
        Facility ID: {parkingSpaceId}
      </p>

      {createdSecret && (
        <div style={{
          ...card,
          borderColor: 'rgba(251,191,36,0.5)',
          background: 'rgba(251,191,36,0.08)',
        }}>
          <strong style={{ color: '#fbbf24' }}>Save this API secret now</strong>
          <pre style={{
            marginTop: 8,
            padding: 12,
            background: '#0f172a',
            borderRadius: 8,
            overflow: 'auto',
            color: '#e2e8f0',
          }}>{createdSecret}</pre>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              navigator.clipboard?.writeText(createdSecret);
              toast.success('Copied');
            }}
          >
            Copy secret
          </button>
        </div>
      )}

      {loading ? (
        <div className="loading"><div className="spinner" /></div>
      ) : (
        <>
          <section style={card}>
            <h2 style={{ fontSize: '1.1rem', marginBottom: '1rem' }}>Camera API keys</h2>
            <form onSubmit={createKey} style={{ display: 'grid', gap: 10, marginBottom: 16 }}>
              <input
                className="input"
                placeholder="Name (e.g. North gate camera)"
                value={keyName}
                onChange={(e) => setKeyName(e.target.value)}
              />
              <input
                className="input"
                placeholder="Optional key id (auto-generated if empty)"
                value={keyId}
                onChange={(e) => setKeyId(e.target.value)}
              />
              <button type="submit" className="btn btn-primary" disabled={busy}>Create camera key</button>
            </form>

            {keys.length === 0 ? (
              <p style={{ color: '#94a3b8' }}>No camera keys yet. Create one for your LPR gate.</p>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {keys.map((k) => (
                  <div key={k.id} style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    gap: 8,
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '10px 12px',
                    background: '#0f172a',
                    borderRadius: 8,
                  }}>
                    <div>
                      <div style={{ fontWeight: 600 }}>{k.name}</div>
                      <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
                        {k.keyId} · prefix {k.secretPrefix}… · {k.isEnabled ? 'Enabled' : 'Disabled'}
                      </div>
                    </div>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => toggleKey(k.id, k.isEnabled)}>
                        {k.isEnabled ? 'Disable' : 'Enable'}
                      </button>
                      <button type="button" className="btn btn-danger" disabled={busy} onClick={() => deleteKey(k.id)}>Delete</button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section style={card}>
            <h2 style={{ fontSize: '1.1rem', marginBottom: '0.5rem' }}>Plate allow / deny lists</h2>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem', marginBottom: '1rem' }}>
              <strong>Deny</strong> always blocks the plate. If any <strong>Allow</strong> rules exist, only those plates may use LPR (and still need a booking).
            </p>
            <form onSubmit={createRule} style={{ display: 'grid', gap: 10, marginBottom: 16 }}>
              <input
                className="input"
                placeholder="License plate"
                value={plate}
                onChange={(e) => setPlate(e.target.value)}
              />
              <select className="input" value={ruleType} onChange={(e) => setRuleType(e.target.value)}>
                {RULE_TYPES.map((t) => (
                  <option key={t.value} value={t.value}>{t.label}</option>
                ))}
              </select>
              <input
                className="input"
                placeholder="Optional note"
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
              <button type="submit" className="btn btn-primary" disabled={busy}>Add plate rule</button>
            </form>

            {rules.length === 0 ? (
              <p style={{ color: '#94a3b8' }}>No plate rules. All booked plates can use LPR when enabled.</p>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {rules.map((r) => (
                  <div key={r.id} style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    gap: 8,
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '10px 12px',
                    background: '#0f172a',
                    borderRadius: 8,
                  }}>
                    <div>
                      <span style={{
                        display: 'inline-block',
                        marginRight: 8,
                        padding: '2px 8px',
                        borderRadius: 999,
                        fontSize: '0.75rem',
                        fontWeight: 600,
                        color: r.ruleType === 2 ? '#f87171' : '#34d399',
                        background: r.ruleType === 2 ? 'rgba(248,113,113,0.15)' : 'rgba(52,211,153,0.15)',
                      }}>
                        {r.ruleType === 2 ? 'Deny' : 'Allow'}
                      </span>
                      <strong>{r.licensePlateNormalized}</strong>
                      {r.note && <span style={{ color: '#94a3b8', marginLeft: 8 }}>{r.note}</span>}
                      <div style={{ fontSize: '0.8rem', color: '#64748b' }}>
                        {r.isEnabled ? 'Enabled' : 'Disabled'}
                      </div>
                    </div>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => toggleRule(r.id, r.isEnabled)}>
                        {r.isEnabled ? 'Disable' : 'Enable'}
                      </button>
                      <button type="button" className="btn btn-danger" disabled={busy} onClick={() => deleteRule(r.id)}>Delete</button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}
