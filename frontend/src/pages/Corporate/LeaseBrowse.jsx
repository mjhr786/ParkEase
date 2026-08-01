import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { useCompany } from '../../contexts/CompanyContext';
import api from '../../services/api';
import corporateService from '../../services/corporateService';
import { handleApiError } from '../../utils/errorHandler';
import showToast from '../../utils/toast.jsx';

const defaultAllocationRequest = {
  totalSlots: 1,
  fixedSlots: 0,
  sharedSlots: 1,
  monthlyRate: 0,
  startDate: '',
  endDate: '',
  maxBookingsPerEmployeePerDay: 1,
  maxBookingsPerEmployeePerWeek: 5,
  priorityThreshold: 1,
  allowedStartTime: '07:00',
  allowedEndTime: '22:00',
  allowWeekends: false,
  leaseReference: '',
};

function isCompanyAdminRole(role) {
  return role === 'Admin' || role === 0 || role === '0';
}

/**
 * KD-17 / PR8: Corporate company Admin lease-browse.
 * Allowlisted marketplace search/get only — allocation request, no pay/book.
 */
export default function LeaseBrowse() {
  const navigate = useNavigate();
  const { companyRole } = useAuth();
  const { isCorporateMode, activeCompanyId } = useCompany();

  const [city, setCity] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState('');

  const [selected, setSelected] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [allocationRequest, setAllocationRequest] = useState(defaultAllocationRequest);
  const [submitting, setSubmitting] = useState(false);

  const isCompanyAdmin = isCompanyAdminRole(companyRole);

  useEffect(() => {
    if (!isCorporateMode) {
      navigate('/dashboard', { replace: true });
    }
  }, [isCorporateMode, navigate]);

  const search = useCallback(async (pageOverride) => {
    const nextPage = pageOverride ?? page;
    setLoading(true);
    setSearchError('');
    try {
      const params = {
        page: nextPage,
        pageSize: 12,
        sortBy: 'createdAt',
        sortDescending: true,
      };
      if (city.trim()) params.city = city.trim();

      const res = await api.searchParking(params);
      if (res.success && res.data) {
        setResults(res.data.parkingSpaces || []);
        setTotalPages(res.data.totalPages || 1);
        setPage(nextPage);
      } else {
        setResults([]);
        setSearchError(res.message || 'Search failed');
      }
    } catch (err) {
      setResults([]);
      setSearchError(handleApiError(err, 'Could not search vendor parking'));
    } finally {
      setLoading(false);
    }
  }, [city, page]);

  useEffect(() => {
    if (!isCorporateMode || !isCompanyAdmin) return;
    search(1);
    // Initial load only when admin + corporate mode
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isCorporateMode, isCompanyAdmin, activeCompanyId]);

  const openLot = async (space) => {
    setDetailLoading(true);
    setSelected(null);
    setAllocationRequest({
      ...defaultAllocationRequest,
      totalSlots: Math.min(space.totalSpots || 1, 10),
      sharedSlots: Math.min(space.totalSpots || 1, 10),
      monthlyRate: space.monthlyRate ?? space.hourlyRate ?? 0,
    });
    try {
      const res = await api.getParkingById(space.id);
      if (res.success && res.data) {
        setSelected(res.data);
      } else {
        showToast.error(res.message || 'Could not load parking details');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Could not load parking details'));
    } finally {
      setDetailLoading(false);
    }
  };

  const handleAllocationRequestChange = (field, value) => {
    setAllocationRequest((prev) => {
      const next = { ...prev, [field]: value };
      if (field === 'totalSlots') {
        const total = Math.max(1, parseInt(value, 10) || 1);
        next.totalSlots = total;
        next.fixedSlots = Math.min(parseInt(next.fixedSlots, 10) || 0, total);
        next.sharedSlots = Math.min(parseInt(next.sharedSlots, 10) || 0, total - next.fixedSlots);
      }
      if (field === 'fixedSlots' || field === 'sharedSlots') {
        next[field] = Math.max(0, parseInt(value, 10) || 0);
      }
      return next;
    });
  };

  const handleRequestAllocation = async (e) => {
    e.preventDefault();
    if (!selected?.id) return;

    const totalSlots = parseInt(allocationRequest.totalSlots, 10);
    const fixedSlots = parseInt(allocationRequest.fixedSlots, 10);
    const sharedSlots = parseInt(allocationRequest.sharedSlots, 10);

    if (fixedSlots + sharedSlots > totalSlots) {
      showToast.error('Fixed and shared slots cannot exceed total slots.');
      return;
    }
    if (allocationRequest.allowedEndTime <= allocationRequest.allowedStartTime) {
      showToast.error('Allowed end time must be after allowed start time.');
      return;
    }
    if (!allocationRequest.startDate || !allocationRequest.endDate) {
      showToast.error('Start and end dates are required.');
      return;
    }
    if (allocationRequest.endDate <= allocationRequest.startDate) {
      showToast.error('End date must be after start date.');
      return;
    }

    setSubmitting(true);
    try {
      const response = await corporateService.requestAllocation({
        parkingSpaceId: selected.id,
        totalSlots,
        fixedSlots,
        sharedSlots,
        monthlyRate: parseFloat(allocationRequest.monthlyRate) || 0,
        startDate: new Date(`${allocationRequest.startDate}T00:00:00.000Z`).toISOString(),
        endDate: new Date(`${allocationRequest.endDate}T23:59:59.000Z`).toISOString(),
        leaseReference: allocationRequest.leaseReference || null,
        policy: {
          maxBookingsPerEmployeePerDay: parseInt(allocationRequest.maxBookingsPerEmployeePerDay, 10),
          maxBookingsPerEmployeePerWeek: parseInt(allocationRequest.maxBookingsPerEmployeePerWeek, 10),
          priorityThreshold: parseInt(allocationRequest.priorityThreshold, 10),
          allowedStartTime: `${allocationRequest.allowedStartTime}:00`,
          allowedEndTime: `${allocationRequest.allowedEndTime}:00`,
          allowWeekends: allocationRequest.allowWeekends,
        },
      });

      if (response.success) {
        showToast.success('Allocation request submitted for owner approval.');
        setSelected(null);
        navigate('/corporate/allocations');
      } else {
        showToast.error(response.message || 'Failed to request allocation.');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to request allocation.'));
    } finally {
      setSubmitting(false);
    }
  };

  if (!isCorporateMode) return null;

  if (!isCompanyAdmin) {
    return (
      <div className="container" style={{ padding: '2rem 0' }}>
        <div className="card" style={{ maxWidth: 520, margin: '2rem auto', padding: '1.5rem' }}>
          <h1 style={{ marginTop: 0, color: 'var(--color-text-primary)' }}>Lease browse</h1>
          <p style={{ color: 'var(--color-text-secondary)' }}>
            Only company Admins can browse vendor lots and request leased allocations
            (Admin company role required).
          </p>
          <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
            <Link to="/corporate/allocations" className="btn btn-primary">
              Back to allocations
            </Link>
            <Link to="/corporate/dashboard" className="btn btn-secondary">
              Corporate dashboard
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="container" style={{ padding: '2rem 0', color: 'var(--color-text)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
        <div>
          <h1 style={{ margin: 0, color: 'var(--color-text-primary)', display: 'flex', alignItems: 'center', gap: 10 }}>
            <span aria-hidden>🔍</span> Lease browse
          </h1>
          <p style={{ margin: '0.4rem 0 0', color: 'var(--color-text-secondary)', maxWidth: 640 }}>
            Search public vendor parking and submit a lease (allocation) request. Marketplace pay/book is not available here.
          </p>
        </div>
        <Link to="/corporate/allocations" className="btn btn-secondary">
          View allocations
        </Link>
      </div>

      <div className="card" style={{ padding: '1.25rem', marginBottom: '1.25rem' }}>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            search(1);
          }}
          style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', alignItems: 'flex-end' }}
        >
          <div className="form-group" style={{ margin: 0, flex: '1 1 200px' }}>
            <label className="form-label" htmlFor="lease-browse-city">City (optional)</label>
            <input
              id="lease-browse-city"
              type="text"
              className="form-input"
              value={city}
              onChange={(e) => setCity(e.target.value)}
              placeholder="e.g. Mumbai"
            />
          </div>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Searching…' : 'Search vendor lots'}
          </button>
        </form>
        {searchError && (
          <div className="alert alert-error" style={{ marginTop: '1rem' }} role="alert">
            {searchError}
          </div>
        )}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: selected || detailLoading ? '1fr 1.1fr' : '1fr', gap: '1.25rem', alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          {loading && results.length === 0 ? (
            <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner" /></div>
          ) : results.length === 0 ? (
            <div className="card" style={{ padding: '2rem', textAlign: 'center', color: 'var(--color-text-secondary)' }}>
              No vendor lots found. Try another city or clear filters.
            </div>
          ) : (
            results.map((space) => {
              const active = selected?.id === space.id;
              return (
                <button
                  key={space.id}
                  type="button"
                  onClick={() => openLot(space)}
                  style={{
                    textAlign: 'left',
                    background: active ? 'rgba(56, 189, 248, 0.1)' : 'var(--color-surface)',
                    border: active ? '1px solid rgba(56, 189, 248, 0.45)' : '1px solid var(--color-border)',
                    borderRadius: 12,
                    padding: '1rem 1.15rem',
                    color: 'var(--color-text-primary)',
                    cursor: 'pointer',
                  }}
                >
                  <div style={{ fontWeight: 700, marginBottom: 4 }}>{space.title}</div>
                  <div style={{ fontSize: '0.85rem', color: 'var(--color-text-secondary)' }}>
                    {[space.address, space.city, space.state].filter(Boolean).join(', ')}
                  </div>
                  <div style={{ marginTop: 8, fontSize: '0.82rem', color: 'var(--color-text-muted)' }}>
                    {space.totalSpots != null ? `${space.totalSpots} spots` : '—'}
                    {space.hourlyRate != null ? ` · ₹${space.hourlyRate}/hr` : ''}
                    {space.averageRating != null ? ` · ★ ${Number(space.averageRating).toFixed(1)}` : ''}
                  </div>
                </button>
              );
            })
          )}

          {totalPages > 1 && (
            <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'center', marginTop: '0.5rem' }}>
              <button
                type="button"
                className="btn btn-secondary"
                disabled={page <= 1 || loading}
                onClick={() => search(page - 1)}
              >
                Previous
              </button>
              <span style={{ alignSelf: 'center', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
                Page {page} / {totalPages}
              </span>
              <button
                type="button"
                className="btn btn-secondary"
                disabled={page >= totalPages || loading}
                onClick={() => search(page + 1)}
              >
                Next
              </button>
            </div>
          )}
        </div>

        {(detailLoading || selected) && (
          <div className="card" style={{ padding: '1.25rem', position: 'sticky', top: 16 }}>
            {detailLoading && !selected ? (
              <div style={{ padding: '2rem', textAlign: 'center' }}><div className="spinner" /></div>
            ) : selected ? (
              <>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: '0.75rem', marginBottom: '1rem' }}>
                  <div>
                    <h2 style={{ margin: 0, fontSize: '1.2rem', color: 'var(--color-text-primary)' }}>{selected.title}</h2>
                    <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
                      {[selected.address, selected.city, selected.state].filter(Boolean).join(', ')}
                    </p>
                    <p style={{ margin: '0.5rem 0 0', color: 'var(--color-text-muted)', fontSize: '0.85rem' }}>
                      {selected.totalSpots} spots · allocation request only (no marketplace booking)
                    </p>
                  </div>
                  <button type="button" className="btn btn-secondary" onClick={() => setSelected(null)}>
                    Close
                  </button>
                </div>

                <form onSubmit={handleRequestAllocation}>
                  <h3 style={{ margin: '0 0 1rem', fontSize: '1rem', color: 'var(--color-text-primary)' }}>
                    Request corporate allocation
                  </h3>
                  <div className="grid grid-3" style={{ gap: '1rem' }}>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-total">Total slots</label>
                      <input id="lb-total" type="number" min="1" max={selected.totalSpots || 1000} className="form-input" value={allocationRequest.totalSlots} onChange={(e) => handleAllocationRequestChange('totalSlots', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-fixed">Fixed slots</label>
                      <input id="lb-fixed" type="number" min="0" className="form-input" value={allocationRequest.fixedSlots} onChange={(e) => handleAllocationRequestChange('fixedSlots', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-shared">Shared slots</label>
                      <input id="lb-shared" type="number" min="0" className="form-input" value={allocationRequest.sharedSlots} onChange={(e) => handleAllocationRequestChange('sharedSlots', e.target.value)} required />
                    </div>
                  </div>

                  <div className="grid grid-3" style={{ gap: '1rem' }}>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-rate">Monthly rate</label>
                      <input id="lb-rate" type="number" min="0" step="0.01" className="form-input" value={allocationRequest.monthlyRate} onChange={(e) => handleAllocationRequestChange('monthlyRate', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-start">Start date</label>
                      <input id="lb-start" type="date" className="form-input" value={allocationRequest.startDate} onChange={(e) => handleAllocationRequestChange('startDate', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-end">End date</label>
                      <input id="lb-end" type="date" className="form-input" value={allocationRequest.endDate} onChange={(e) => handleAllocationRequestChange('endDate', e.target.value)} required />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="form-label" htmlFor="lb-lease-ref">Lease reference</label>
                    <input
                      id="lb-lease-ref"
                      type="text"
                      maxLength={100}
                      className="form-input"
                      placeholder="Optional contract or PO reference"
                      value={allocationRequest.leaseReference}
                      onChange={(e) => handleAllocationRequestChange('leaseReference', e.target.value)}
                    />
                  </div>

                  <div className="grid grid-3" style={{ gap: '1rem' }}>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-max-day">Max/day</label>
                      <input id="lb-max-day" type="number" min="1" className="form-input" value={allocationRequest.maxBookingsPerEmployeePerDay} onChange={(e) => handleAllocationRequestChange('maxBookingsPerEmployeePerDay', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-max-week">Max/week</label>
                      <input id="lb-max-week" type="number" min="1" className="form-input" value={allocationRequest.maxBookingsPerEmployeePerWeek} onChange={(e) => handleAllocationRequestChange('maxBookingsPerEmployeePerWeek', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-priority">Min priority</label>
                      <input id="lb-priority" type="number" min="1" max="10" className="form-input" value={allocationRequest.priorityThreshold} onChange={(e) => handleAllocationRequestChange('priorityThreshold', e.target.value)} required />
                    </div>
                  </div>

                  <div className="grid grid-3" style={{ gap: '1rem' }}>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-hours-start">Allowed start</label>
                      <input id="lb-hours-start" type="time" className="form-input" value={allocationRequest.allowedStartTime} onChange={(e) => handleAllocationRequestChange('allowedStartTime', e.target.value)} required />
                    </div>
                    <div className="form-group">
                      <label className="form-label" htmlFor="lb-hours-end">Allowed end</label>
                      <input id="lb-hours-end" type="time" className="form-input" value={allocationRequest.allowedEndTime} onChange={(e) => handleAllocationRequestChange('allowedEndTime', e.target.value)} required />
                    </div>
                    <div className="form-group" style={{ display: 'flex', alignItems: 'center', paddingTop: '1.8rem' }}>
                      <label style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', cursor: 'pointer' }}>
                        <input
                          type="checkbox"
                          checked={allocationRequest.allowWeekends}
                          onChange={(e) => handleAllocationRequestChange('allowWeekends', e.target.checked)}
                        />
                        Allow weekends
                      </label>
                    </div>
                  </div>

                  <div className="flex gap-1 mt-2" style={{ justifyContent: 'flex-end' }}>
                    <button type="button" className="btn btn-secondary" onClick={() => setSelected(null)}>
                      Cancel
                    </button>
                    <button type="submit" className="btn btn-primary" disabled={submitting}>
                      {submitting ? 'Submitting…' : 'Submit lease request'}
                    </button>
                  </div>
                </form>
              </>
            ) : null}
          </div>
        )}
      </div>
    </div>
  );
}
