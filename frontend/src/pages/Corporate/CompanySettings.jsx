import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';

const BILLING_TYPES = [
  { value: 0, label: 'Reserved Slots' },
  { value: 1, label: 'Usage Based' },
];

const INVITE_STATUS = {
  0: { label: 'Pending', color: '#fbbf24' },
  1: { label: 'Accepted', color: '#10b981' },
  2: { label: 'Expired', color: '#94a3b8' },
  3: { label: 'Cancelled', color: '#f87171' },
};

const CompanySettings = () => {
  const { activeCompanyId, companyDetails, isCorporateMode, refreshCompanyDetails } = useCompany();
  const navigate = useNavigate();

  const [form, setForm] = useState({
    name: '',
    contactEmail: '',
    contactPhone: '',
    billingAddress: '',
    billingType: 0,
    registrationNumber: '',
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [invitations, setInvitations] = useState([]);
  const [loadingInvites, setLoadingInvites] = useState(true);

  useEffect(() => {
    if (!isCorporateMode) {
      navigate('/dashboard', { replace: true });
    }
  }, [isCorporateMode, navigate]);

  useEffect(() => {
    if (!isCorporateMode) return;

    const load = async () => {
      setLoading(true);
      try {
        const res = await corporateService.getCompany();
        if (res.success && res.data) {
          const c = res.data;
          setForm({
            name: c.name || '',
            contactEmail: c.contactEmail || '',
            contactPhone: c.contactPhone || '',
            billingAddress: c.billingAddress || '',
            billingType: c.billingType ?? 0,
            registrationNumber: c.registrationNumber || '',
          });
        } else {
          toast.error(res.message || 'Failed to load company');
        }
      } catch {
        toast.error('Could not reach server');
      } finally {
        setLoading(false);
      }
    };

    const loadInvites = async () => {
      setLoadingInvites(true);
      try {
        const res = await corporateService.getInvitations();
        if (res.success && res.data) {
          setInvitations(res.data);
        } else {
          setInvitations([]);
        }
      } catch {
        setInvitations([]);
      } finally {
        setLoadingInvites(false);
      }
    };

    load();
    loadInvites();
  }, [activeCompanyId, isCorporateMode]);

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      const payload = {
        name: form.name.trim(),
        contactEmail: form.contactEmail.trim(),
        contactPhone: form.contactPhone.trim(),
        billingAddress: form.billingAddress.trim(),
        billingType: parseInt(form.billingType, 10),
      };
      const res = await corporateService.updateCompany(payload);
      if (res.success) {
        toast.success(res.message || 'Company updated');
        if (refreshCompanyDetails) await refreshCompanyDetails();
      } else {
        toast.error(res.message || 'Update failed');
      }
    } catch (err) {
      toast.error(err?.message || 'Update failed');
    } finally {
      setSaving(false);
    }
  };

  const handleCancelInvite = async (id) => {
    if (!window.confirm('Cancel this invitation?')) return;
    try {
      const res = await corporateService.cancelInvitation(id);
      if (res.success) {
        toast.success('Invitation cancelled');
        setInvitations((prev) => prev.map((i) => (i.id === id ? { ...i, status: 3 } : i)));
      } else {
        toast.error(res.message || 'Failed to cancel invitation');
      }
    } catch (err) {
      toast.error(err?.message || 'Failed to cancel invitation');
    }
  };

  if (!isCorporateMode) return null;

  if (loading) {
    return (
      <div className="loading" style={{ minHeight: '60vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
        <div className="spinner" />
      </div>
    );
  }

  const pendingInvites = invitations.filter((i) => i.status === 0);

  return (
    <div className="container" style={{ padding: '2rem 0', color: '#f1f5f9' }}>
      <h1 style={{ margin: '0 0 0.35rem 0', color: 'white', display: 'flex', alignItems: 'center', gap: '10px' }}>
        <span style={{ fontSize: '2rem' }}>⚙️</span> Company Settings
      </h1>
      <p style={{ color: '#94a3b8', marginBottom: '1.75rem' }}>
        {companyDetails?.name || form.name} · Update profile, billing type, and manage pending invitations.
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.5rem' }}>
        <form
          onSubmit={handleSave}
          style={{
            background: '#1e293b',
            borderRadius: '12px',
            padding: '1.5rem',
            border: '1px solid rgba(255,255,255,0.05)',
          }}
        >
          <h2 style={{ color: 'white', fontSize: '1.1rem', margin: '0 0 1.25rem 0' }}>Profile</h2>

          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>Company name</label>
            <input
              required
              minLength={3}
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
            />
          </div>

          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>Registration number</label>
            <input
              value={form.registrationNumber}
              disabled
              style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: '#64748b' }}
            />
            <small style={{ color: '#64748b' }}>Registration number cannot be changed after create.</small>
          </div>

          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>Contact email</label>
            <input
              type="email"
              required
              value={form.contactEmail}
              onChange={(e) => setForm({ ...form, contactEmail: e.target.value })}
              style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
            />
          </div>

          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>Contact phone</label>
            <input
              required
              value={form.contactPhone}
              onChange={(e) => setForm({ ...form, contactPhone: e.target.value })}
              style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
            />
          </div>

          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>Billing address</label>
            <textarea
              required
              rows={3}
              value={form.billingAddress}
              onChange={(e) => setForm({ ...form, billingAddress: e.target.value })}
              style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white', resize: 'vertical' }}
            />
          </div>

          <div className="form-group" style={{ marginBottom: '1.5rem' }}>
            <label style={{ display: 'block', marginBottom: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>Billing type</label>
            <select
              value={form.billingType}
              onChange={(e) => setForm({ ...form, billingType: e.target.value })}
              style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
            >
              {BILLING_TYPES.map((b) => (
                <option key={b.value} value={b.value}>{b.label}</option>
              ))}
            </select>
            <small style={{ color: '#64748b', display: 'block', marginTop: '4px' }}>
              Reserved Slots bills prepaid capacity; Usage Based keeps amounts for reporting.
            </small>
          </div>

          <button type="submit" className="btn btn-primary" disabled={saving}>
            {saving ? 'Saving…' : 'Save company'}
          </button>
        </form>

        <div
          style={{
            background: '#1e293b',
            borderRadius: '12px',
            padding: '1.5rem',
            border: '1px solid rgba(255,255,255,0.05)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', gap: '0.75rem', flexWrap: 'wrap' }}>
            <h2 style={{ color: 'white', fontSize: '1.1rem', margin: 0 }}>
              Invitations
              {pendingInvites.length > 0 && (
                <span style={{ marginLeft: '8px', fontSize: '0.8rem', color: '#fbbf24' }}>
                  ({pendingInvites.length} pending)
                </span>
              )}
            </h2>
            <button type="button" className="btn btn-secondary" onClick={() => navigate('/corporate/members')}>
              Manage members
            </button>
          </div>

          {loadingInvites ? (
            <div style={{ padding: '2rem', textAlign: 'center' }}><div className="spinner" /></div>
          ) : invitations.length === 0 ? (
            <p style={{ color: '#94a3b8', margin: 0 }}>No invitations yet. Invite people from the Members page.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>
              {invitations.map((inv) => {
                const st = INVITE_STATUS[inv.status] || INVITE_STATUS[0];
                return (
                  <div
                    key={inv.id}
                    style={{
                      display: 'grid',
                      gridTemplateColumns: '1.2fr 0.8fr auto',
                      gap: '0.75rem',
                      alignItems: 'center',
                      padding: '0.75rem 0.85rem',
                      background: 'rgba(255,255,255,0.03)',
                      borderRadius: '8px',
                    }}
                  >
                    <div>
                      <div style={{ color: '#e2e8f0', fontWeight: 600 }}>{inv.email}</div>
                      <div style={{ color: '#64748b', fontSize: '0.78rem' }}>
                        {inv.role === 0 ? 'Admin' : 'Employee'} · expires {new Date(inv.expiresAt).toLocaleDateString()}
                      </div>
                    </div>
                    <span style={{ color: st.color, fontWeight: 700, fontSize: '0.85rem' }}>{st.label}</span>
                    {(inv.status === 0 || inv.status === 2) ? (
                      <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                        {inv.invitationToken && inv.status === 0 && (
                          <button
                            type="button"
                            className="btn btn-primary"
                            style={{ padding: '0.35rem 0.7rem', fontSize: '0.8rem' }}
                            onClick={async () => {
                              const link = `${window.location.origin}/invite/accept/${encodeURIComponent(inv.invitationToken)}`;
                              try {
                                await navigator.clipboard.writeText(link);
                                toast.success('Invite link copied');
                              } catch {
                                toast.error('Could not copy link');
                              }
                            }}
                          >
                            Copy link
                          </button>
                        )}
                        <button
                          type="button"
                          className="btn btn-secondary"
                          style={{ padding: '0.35rem 0.7rem', fontSize: '0.8rem' }}
                          onClick={async () => {
                            try {
                              const res = await corporateService.resendInvitation(inv.id);
                              if (res.success) {
                                toast.success(res.message || 'Invitation resent');
                                const token = res.data?.invitationToken;
                                if (token) {
                                  const link = `${window.location.origin}/invite/accept/${encodeURIComponent(token)}`;
                                  try {
                                    await navigator.clipboard.writeText(link);
                                    toast.success('New invite link copied');
                                  } catch { /* ignore */ }
                                }
                                // reload list
                                const list = await corporateService.getInvitations();
                                if (list.success && list.data) setInvitations(list.data);
                              } else {
                                toast.error(res.message || 'Resend failed');
                              }
                            } catch (err) {
                              toast.error(err?.message || 'Resend failed');
                            }
                          }}
                        >
                          Resend
                        </button>
                        {inv.status === 0 && (
                          <button
                            type="button"
                            className="btn btn-secondary"
                            style={{ padding: '0.35rem 0.7rem', fontSize: '0.8rem' }}
                            onClick={() => handleCancelInvite(inv.id)}
                          >
                            Cancel
                          </button>
                        )}
                      </div>
                    ) : (
                      <span />
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default CompanySettings;
