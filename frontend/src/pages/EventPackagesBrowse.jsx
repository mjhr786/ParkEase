import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import api from '../services/api';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';

const fmtRange = (start, end) =>
  `${new Date(start).toLocaleString()} → ${new Date(end).toLocaleString()}`;

export default function EventPackagesBrowse() {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [venues, setVenues] = useState([]);
  const [loading, setLoading] = useState(true);
  const [buyingId, setBuyingId] = useState(null);
  const [vehicleNumber, setVehicleNumber] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getEventVenuesOnSale(50);
      if (res.success) setVenues(res.data || []);
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to load event packages'));
    }
    setLoading(false);
  }, []);

  useEffect(() => { load(); }, [load]);

  const handlePurchase = async (packageId) => {
    if (!isAuthenticated) {
      navigate('/login?returnUrl=/events');
      return;
    }
    setBuyingId(packageId);
    try {
      const res = await api.purchaseEventPackage(packageId, {
        vehicleType: 0,
        vehicleNumber: vehicleNumber || null,
      });
      if (res.success) {
        showToast.success(res.message || 'Package reserved — ticket PDF emails after payment');
        navigate('/bookings');
      } else {
        showToast.error(res.message || 'Purchase failed');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Purchase failed'));
    }
    setBuyingId(null);
  };

  return (
    <div className="page">
      <div className="container">
        <h1 className="page-title">Event parking</h1>
        <p className="card-subtitle" style={{ marginBottom: '1.5rem' }}>
          Prepaid packages for concerts, games, and special events — multi-lot zones, fixed price.
        </p>

        <div className="card mb-3" style={{ maxWidth: 420 }}>
          <label className="form-label">Plate for booking (optional)</label>
          <input
            className="form-input"
            placeholder="KA01AB1234"
            value={vehicleNumber}
            onChange={(e) => setVehicleNumber(e.target.value)}
          />
        </div>

        {loading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : venues.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon">🎟️</div>
            <h3>No event packages on sale</h3>
            <p>Check back later or <Link to="/search">search regular parking</Link>.</p>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            {venues.map((venue) => (
              <div key={venue.venueEventId} className="card">
                <div className="flex-between" style={{ flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div>
                    <h2 className="card-title" style={{ margin: 0 }}>
                      {venue.eventName || 'Event parking'}
                    </h2>
                    {venue.venueName && (
                      <div style={{ color: '#94a3b8', marginTop: '0.25rem' }}>{venue.venueName}</div>
                    )}
                  </div>
                  <div style={{ textAlign: 'right', fontSize: '0.9rem', color: '#94a3b8' }}>
                    <div>
                      {venue.zoneCount > 1
                        ? `₹${Number(venue.minPackagePrice).toFixed(0)}–${Number(venue.maxPackagePrice).toFixed(0)}`
                        : `₹${Number(venue.minPackagePrice).toFixed(0)}`}
                    </div>
                    <div>{venue.totalAvailableSpots} spots · {venue.zoneCount} zone{venue.zoneCount === 1 ? '' : 's'}</div>
                  </div>
                </div>
                <div style={{ marginTop: '0.5rem', fontSize: '0.9rem' }}>
                  Showtime: {fmtRange(venue.eventStartUtc, venue.eventEndUtc)}
                </div>

                <div className="grid grid-2" style={{ marginTop: '1rem', gap: '0.75rem' }}>
                  {(venue.zones || []).map((pkg) => (
                    <div
                      key={pkg.id}
                      style={{
                        border: '1px solid rgba(148,163,184,0.25)',
                        borderRadius: 10,
                        padding: '0.85rem',
                      }}
                    >
                      <div className="flex-between">
                        <strong>{pkg.zoneName || pkg.title}</strong>
                        <span className="parking-tag" style={{ background: 'rgba(99,102,241,0.2)', color: '#a5b4fc' }}>
                          ₹{Number(pkg.packagePrice).toFixed(0)}
                        </span>
                      </div>
                      <div style={{ marginTop: '0.5rem', fontSize: '0.85rem', color: '#94a3b8' }}>
                        <div>📍 {pkg.parkingSpaceTitle}{pkg.parkingSpaceCity ? `, ${pkg.parkingSpaceCity}` : ''}</div>
                        <div>
                          Access:{' '}
                          {fmtRange(
                            pkg.accessStartUtc || pkg.eventStartUtc,
                            pkg.accessEndUtc || pkg.eventEndUtc,
                          )}
                        </div>
                        {(pkg.earlyEntryMinutes > 0 || pkg.lateExitMinutes > 0) && (
                          <div>
                            Buffers: early {pkg.earlyEntryMinutes || 0}m · late {pkg.lateExitMinutes || 0}m
                          </div>
                        )}
                        <div>🎫 {pkg.availableSpots} left of {pkg.totalSpots}</div>
                      </div>
                      {pkg.description && (
                        <p style={{ fontSize: '0.8rem', color: '#94a3b8', marginTop: '0.5rem' }}>{pkg.description}</p>
                      )}
                      <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.75rem', flexWrap: 'wrap' }}>
                        <button
                          type="button"
                          className="btn btn-primary"
                          disabled={buyingId === pkg.id || !pkg.isOnSale}
                          onClick={() => handlePurchase(pkg.id)}
                        >
                          {buyingId === pkg.id ? 'Reserving…' : 'Buy zone'}
                        </button>
                        <Link to={`/parking/${pkg.parkingSpaceId}`} className="btn btn-secondary">
                          View lot
                        </Link>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
