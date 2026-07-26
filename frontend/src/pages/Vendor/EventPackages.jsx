import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import api from '../../services/api';
import { handleApiError } from '../../utils/errorHandler';
import showToast from '../../utils/toast.jsx';

const emptyForm = {
  parkingSpaceId: '',
  title: '',
  eventName: '',
  venueName: '',
  zoneName: '',
  venueEventId: '',
  description: '',
  eventStartUtc: '',
  eventEndUtc: '',
  packagePrice: 500,
  totalSpots: 20,
  earlyEntryMinutes: 0,
  lateExitMinutes: 0,
};

const toLocalInput = (iso) => {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

export default function EventPackages() {
  const [listings, setListings] = useState([]);
  const [packages, setPackages] = useState([]);
  const [analytics, setAnalytics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [submitting, setSubmitting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [listRes, pkgRes, analyticsRes] = await Promise.all([
        api.getMyListings(),
        api.getMyEventPackages(),
        api.getMyEventPackageAnalytics(),
      ]);
      if (listRes.success) setListings(listRes.data || []);
      if (pkgRes.success) setPackages(pkgRes.data || []);
      if (analyticsRes.success) setAnalytics(analyticsRes.data || []);
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to load event packages'));
    }
    setLoading(false);
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.parkingSpaceId || !form.title || !form.eventStartUtc || !form.eventEndUtc) {
      showToast.error('Facility, title, and event window are required');
      return;
    }
    setSubmitting(true);
    try {
      const payload = {
        parkingSpaceId: form.parkingSpaceId,
        title: form.title,
        eventName: form.eventName || null,
        venueName: form.venueName || null,
        zoneName: form.zoneName || null,
        description: form.description || null,
        eventStartUtc: new Date(form.eventStartUtc).toISOString(),
        eventEndUtc: new Date(form.eventEndUtc).toISOString(),
        packagePrice: parseFloat(form.packagePrice) || 0,
        totalSpots: parseInt(form.totalSpots, 10) || 1,
        earlyEntryMinutes: parseInt(form.earlyEntryMinutes, 10) || 0,
        lateExitMinutes: parseInt(form.lateExitMinutes, 10) || 0,
      };
      if (form.venueEventId && form.venueEventId.trim()) {
        payload.venueEventId = form.venueEventId.trim();
      }
      const res = await api.createEventPackage(payload);
      if (res.success) {
        showToast.success(
          res.data?.venueEventId
            ? `Package created · venue event ${res.data.venueEventId.slice(0, 8)}…`
            : 'Event package created',
        );
        setShowForm(false);
        setForm(emptyForm);
        await load();
      } else {
        showToast.error(res.message || 'Create failed');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to create package'));
    }
    setSubmitting(false);
  };

  const handleDeactivate = async (id) => {
    if (!window.confirm('Deactivate this event package?')) return;
    try {
      const res = await api.deactivateEventPackage(id);
      if (res.success) {
        showToast.success('Package deactivated');
        await load();
      } else {
        showToast.error(res.message || 'Failed');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to deactivate'));
    }
  };

  const reuseVenueEvent = (venueEventId, eventName, venueName, eventStartUtc, eventEndUtc) => {
    setForm((f) => ({
      ...f,
      venueEventId: venueEventId || '',
      eventName: eventName || f.eventName,
      venueName: venueName || f.venueName,
      eventStartUtc: eventStartUtc ? toLocalInput(eventStartUtc) : f.eventStartUtc,
      eventEndUtc: eventEndUtc ? toLocalInput(eventEndUtc) : f.eventEndUtc,
    }));
    setShowForm(true);
    showToast.success('Venue event id filled — pick another lot/zone and create');
  };

  return (
    <div className="page">
      <div className="container">
        <div className="flex-between mb-3" style={{ flexWrap: 'wrap', gap: '1rem' }}>
          <div>
            <h1 style={{ margin: '0 0 0.25rem 0' }}>Event parking packages</h1>
            <p style={{ margin: 0, color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>
              Multi-lot venue zones, entry buffers, sell-through analytics. Ticket PDF emails after payment.
            </p>
          </div>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <Link to="/my/listings" className="btn btn-secondary">My listings</Link>
            <button type="button" className="btn btn-primary" onClick={() => setShowForm((v) => !v)}>
              {showForm ? 'Close form' : '+ New package'}
            </button>
          </div>
        </div>

        {analytics.length > 0 && (
          <div className="card mb-3">
            <h3 className="card-title">Sell-through analytics</h3>
            <div className="grid grid-2" style={{ gap: '0.75rem' }}>
              {analytics.map((v) => (
                <div
                  key={v.venueEventId}
                  style={{
                    border: '1px solid rgba(148,163,184,0.2)',
                    borderRadius: 10,
                    padding: '0.75rem',
                  }}
                >
                  <div className="flex-between">
                    <strong>{v.eventName || 'Event'}</strong>
                    <span style={{ color: 'var(--color-success)' }}>{v.sellThroughPercent}% sold</span>
                  </div>
                  <div style={{ fontSize: '0.85rem', color: 'var(--color-text-secondary)', marginTop: '0.35rem' }}>
                    {v.venueName || 'Venue'} · {v.zoneCount} zone{v.zoneCount === 1 ? '' : 's'}
                  </div>
                  <div style={{ marginTop: '0.5rem', fontSize: '0.9rem' }}>
                    Sold {v.soldCount}/{v.totalSpots} · Revenue ₹{Number(v.grossRevenue).toFixed(0)}
                  </div>
                  <button
                    type="button"
                    className="btn btn-outline mt-2"
                    style={{ fontSize: '0.8rem' }}
                    onClick={() =>
                      reuseVenueEvent(
                        v.venueEventId,
                        v.eventName,
                        v.venueName,
                        v.eventStartUtc,
                        v.eventEndUtc,
                      )
                    }
                  >
                    + Add zone to this event
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {showForm && (
          <div className="card mb-3">
            <h3 className="card-title">Create package / zone</h3>
            <form onSubmit={handleSubmit}>
              <div className="form-group">
                <label className="form-label">Facility *</label>
                <select
                  className="form-select"
                  value={form.parkingSpaceId}
                  onChange={(e) => setForm({ ...form, parkingSpaceId: e.target.value })}
                  required
                >
                  <option value="">Select listing</option>
                  {listings.map((l) => (
                    <option key={l.id} value={l.id}>{l.title}</option>
                  ))}
                </select>
              </div>
              <div className="grid grid-2" style={{ gap: '1rem' }}>
                <div className="form-group">
                  <label className="form-label">Package title *</label>
                  <input className="form-input" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label className="form-label">Zone name</label>
                  <input
                    className="form-input"
                    placeholder="e.g. VIP Garage"
                    value={form.zoneName}
                    onChange={(e) => setForm({ ...form, zoneName: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">Event name</label>
                  <input className="form-input" value={form.eventName} onChange={(e) => setForm({ ...form, eventName: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Venue</label>
                  <input className="form-input" value={form.venueName} onChange={(e) => setForm({ ...form, venueName: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Reuse venue event id</label>
                  <input
                    className="form-input"
                    placeholder="Paste to add another lot to same event"
                    value={form.venueEventId}
                    onChange={(e) => setForm({ ...form, venueEventId: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">Package price (₹ base) *</label>
                  <input type="number" min="0" className="form-input" value={form.packagePrice} onChange={(e) => setForm({ ...form, packagePrice: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label className="form-label">Showtime start *</label>
                  <input type="datetime-local" className="form-input" value={form.eventStartUtc} onChange={(e) => setForm({ ...form, eventStartUtc: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label className="form-label">Showtime end *</label>
                  <input type="datetime-local" className="form-input" value={form.eventEndUtc} onChange={(e) => setForm({ ...form, eventEndUtc: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label className="form-label">Early entry (minutes)</label>
                  <input
                    type="number"
                    min="0"
                    max="1440"
                    className="form-input"
                    value={form.earlyEntryMinutes}
                    onChange={(e) => setForm({ ...form, earlyEntryMinutes: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">Late exit (minutes)</label>
                  <input
                    type="number"
                    min="0"
                    max="1440"
                    className="form-input"
                    value={form.lateExitMinutes}
                    onChange={(e) => setForm({ ...form, lateExitMinutes: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">Spots to sell *</label>
                  <input type="number" min="1" className="form-input" value={form.totalSpots} onChange={(e) => setForm({ ...form, totalSpots: e.target.value })} required />
                </div>
              </div>
              <div className="form-group">
                <label className="form-label">Description</label>
                <textarea className="form-input" rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
              </div>
              <button type="submit" className="btn btn-primary" disabled={submitting}>
                {submitting ? 'Saving…' : 'Create package'}
              </button>
            </form>
          </div>
        )}

        {loading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : packages.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon">🎟️</div>
            <h3>No event packages yet</h3>
            <p>Create a prepaid package for your next event night.</p>
          </div>
        ) : (
          <div className="grid grid-2">
            {packages.map((pkg) => (
              <div key={pkg.id} className="card">
                <div className="flex-between">
                  <h3 className="card-title" style={{ margin: 0 }}>
                    {pkg.zoneName ? `${pkg.zoneName} · ` : ''}{pkg.title}
                  </h3>
                  <span className="parking-tag" style={{
                    background: pkg.isOnSale ? 'rgba(16,185,129,0.2)' : 'rgba(107,114,128,0.2)',
                    color: pkg.isOnSale ? 'var(--color-success)' : 'var(--color-text-muted)',
                  }}>
                    {pkg.isOnSale ? 'On sale' : pkg.isActive ? 'Inactive/ended' : 'Off'}
                  </span>
                </div>
                <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem', marginTop: '0.35rem' }}>
                  {pkg.parkingSpaceTitle} · {pkg.eventName || 'Event'}
                </div>
                <div style={{ marginTop: '0.75rem', fontSize: '0.9rem' }}>
                  <div>Show: {new Date(pkg.eventStartUtc).toLocaleString()} → {new Date(pkg.eventEndUtc).toLocaleString()}</div>
                  <div>
                    Access:{' '}
                    {new Date(pkg.accessStartUtc || pkg.eventStartUtc).toLocaleString()}
                    {' → '}
                    {new Date(pkg.accessEndUtc || pkg.eventEndUtc).toLocaleString()}
                  </div>
                  <div>Price: ₹{Number(pkg.packagePrice).toFixed(2)} base · Sold {pkg.soldCount}/{pkg.totalSpots}</div>
                  <div style={{ fontSize: '0.8rem', color: 'var(--color-text-muted)', marginTop: '0.35rem' }}>
                    Venue event: <code style={{ fontSize: '0.75rem' }}>{pkg.venueEventId}</code>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.75rem', flexWrap: 'wrap' }}>
                  {pkg.isActive && (
                    <button type="button" className="btn btn-outline" onClick={() => handleDeactivate(pkg.id)}>
                      Deactivate
                    </button>
                  )}
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() =>
                      reuseVenueEvent(
                        pkg.venueEventId,
                        pkg.eventName,
                        pkg.venueName,
                        pkg.eventStartUtc,
                        pkg.eventEndUtc,
                      )
                    }
                  >
                    + Zone for same event
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
