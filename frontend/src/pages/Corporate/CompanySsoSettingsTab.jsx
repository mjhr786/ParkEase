import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';
import { handleApiError } from '../../utils/errorHandler';
import showToast from '../../utils/toast.jsx';

const fieldStyle = {
  width: '100%',
  padding: '10px',
  background: 'var(--color-bg-primary)',
  border: '1px solid var(--color-border)',
  borderRadius: '6px',
  color: 'var(--color-text-primary)',
};

const labelStyle = {
  display: 'block',
  marginBottom: '6px',
  color: 'var(--color-text-secondary)',
  fontSize: '0.85rem',
};

const cardStyle = {
  background: 'var(--color-surface)',
  borderRadius: '12px',
  padding: '1.5rem',
  border: '1px solid var(--color-border)',
};

const badgeBase = {
  display: 'inline-flex',
  alignItems: 'center',
  padding: '0.2rem 0.55rem',
  borderRadius: '999px',
  fontSize: '0.75rem',
  fontWeight: 700,
  letterSpacing: '0.02em',
};

function isCompanyAdminRole(role) {
  return role === 'Admin' || role === 0 || role === '0';
}

function emptyForm() {
  return {
    authority: '',
    clientId: '',
    clientSecret: '',
    metadataUrl: '',
    tokenEndpointAuthMethod: 'client_secret_post',
    passwordLoginAllowed: true,
    autoAcceptInvitationsOnSso: true,
    forceAuthn: false,
    attributeMappingJson: '',
  };
}

function applyConfigToForm(cfg) {
  if (!cfg) return emptyForm();
  return {
    authority: cfg.authority || '',
    clientId: cfg.clientId || '',
    clientSecret: '',
    metadataUrl: cfg.metadataUrl || '',
    tokenEndpointAuthMethod: cfg.tokenEndpointAuthMethod || 'client_secret_post',
    passwordLoginAllowed: cfg.passwordLoginAllowed !== false,
    autoAcceptInvitationsOnSso: cfg.autoAcceptInvitationsOnSso !== false,
    forceAuthn: !!cfg.forceAuthn,
    attributeMappingJson: cfg.attributeMappingJson || '',
  };
}

function StatusBadge({ config }) {
  if (!config) return null;

  if (config.forceDisabledByPlatform) {
    return (
      <span style={{ ...badgeBase, background: 'rgba(239,68,68,0.15)', color: 'var(--color-error)' }}>
        Force-disabled by platform
      </span>
    );
  }
  if (config.isEffectivelyEnabled) {
    return (
      <span style={{ ...badgeBase, background: 'rgba(34,197,94,0.15)', color: 'var(--color-success)' }}>
        SSO active
      </span>
    );
  }
  if (config.isEnabled) {
    return (
      <span style={{ ...badgeBase, background: 'rgba(234,179,8,0.18)', color: 'var(--color-warning)' }}>
        Enabled (not fully effective)
      </span>
    );
  }
  return (
    <span style={{ ...badgeBase, background: 'var(--color-row-elevated)', color: 'var(--color-text-secondary)' }}>
      Disabled
    </span>
  );
}

/**
 * Company Settings → SSO tab (PR7).
 * Company Admin configures OIDC, domains, policies, test/enable/disable, audit.
 */
export default function CompanySsoSettingsTab() {
  const { companyRole } = useAuth();
  const { activeCompanyId, companyDetails, refreshCompanyDetails } = useCompany();
  const isCompanyAdmin = isCompanyAdminRole(companyRole);

  const [loading, setLoading] = useState(true);
  const [config, setConfig] = useState(null);
  const [form, setForm] = useState(emptyForm());
  const [newDomain, setNewDomain] = useState('');
  const [slug, setSlug] = useState('');
  const [confirmSsoOnly, setConfirmSsoOnly] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [showAudit, setShowAudit] = useState(false);
  const [auditEvents, setAuditEvents] = useState([]);
  const [auditLoading, setAuditLoading] = useState(false);

  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [enabling, setEnabling] = useState(false);
  const [disabling, setDisabling] = useState(false);
  const [domainBusyId, setDomainBusyId] = useState(null);
  const [addingDomain, setAddingDomain] = useState(false);
  const [savingSlug, setSavingSlug] = useState(false);

  const loadConfig = useCallback(async () => {
    if (!activeCompanyId || !isCompanyAdmin) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const res = await corporateService.getSsoConfig();
      if (res?.success && res.data) {
        setConfig(res.data);
        setForm(applyConfigToForm(res.data));
        setConfirmSsoOnly(false);
      } else {
        showToast.error(res?.message || 'Failed to load SSO configuration');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to load SSO configuration'));
    } finally {
      setLoading(false);
    }
  }, [activeCompanyId, isCompanyAdmin]);

  useEffect(() => {
    loadConfig();
  }, [loadConfig]);

  useEffect(() => {
    setSlug(companyDetails?.slug || '');
  }, [companyDetails?.slug]);

  const verifiedDomainCount = useMemo(
    () => (config?.domains || []).filter((d) => d.isVerified).length,
    [config]
  );

  const lastTestOk = useMemo(() => {
    const r = (config?.lastTestResult || '').toLowerCase();
    return r === 'ok' || r === 'success';
  }, [config?.lastTestResult]);

  const ssoOnlyReady = verifiedDomainCount >= 1 && lastTestOk;

  const loginUrl = useMemo(() => {
    if (!slug) return null;
    return `${window.location.origin}/corporate/login?company=${encodeURIComponent(slug)}`;
  }, [slug]);

  const copyText = async (text, successMsg) => {
    try {
      await navigator.clipboard.writeText(text);
      showToast.success(successMsg || 'Copied');
    } catch {
      showToast.error('Could not copy to clipboard');
    }
  };

  const handleSaveConfig = async (e) => {
    e.preventDefault();
    if (!isCompanyAdmin) return;

    const turningSsoOnly = form.passwordLoginAllowed === false && config?.passwordLoginAllowed !== false;
    if (turningSsoOnly) {
      if (!ssoOnlyReady) {
        showToast.error(
          'SSO-only requires at least one verified domain and a successful connection test.'
        );
        return;
      }
      if (!confirmSsoOnly) {
        showToast.error('Confirm the SSO-only checkbox before disabling password login.');
        return;
      }
    }

    setSaving(true);
    try {
      const payload = {
        protocol: 'Oidc',
        authority: form.authority.trim(),
        clientId: form.clientId.trim(),
        metadataUrl: form.metadataUrl.trim() || null,
        tokenEndpointAuthMethod: form.tokenEndpointAuthMethod || null,
        passwordLoginAllowed: form.passwordLoginAllowed,
        autoAcceptInvitationsOnSso: form.autoAcceptInvitationsOnSso,
        forceAuthn: form.forceAuthn,
        allowJitProvisioning: false,
        attributeMappingJson: form.attributeMappingJson.trim() || null,
        confirmSsoOnly: turningSsoOnly ? confirmSsoOnly : false,
      };
      // Write-only: omit secret when blank so backend leaves existing value unchanged.
      if (form.clientSecret.trim()) {
        payload.clientSecret = form.clientSecret.trim();
      }

      const res = await corporateService.upsertSsoConfig(payload);
      if (res?.success && res.data) {
        setConfig(res.data);
        setForm(applyConfigToForm(res.data));
        setConfirmSsoOnly(false);
        showToast.success(res.message || 'SSO configuration saved');
      } else {
        showToast.error(res?.message || 'Failed to save SSO configuration');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Failed to save SSO configuration'));
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    setTesting(true);
    try {
      const res = await corporateService.testSso();
      if (res?.success) {
        showToast.success(res.message || 'OIDC discovery succeeded');
      } else {
        showToast.error(res?.message || res?.data?.message || 'Connection test failed');
      }
      await loadConfig();
    } catch (err) {
      showToast.error(handleApiError(err, 'Connection test failed'));
      await loadConfig();
    } finally {
      setTesting(false);
    }
  };

  const handleEnable = async () => {
    setEnabling(true);
    try {
      const res = await corporateService.enableSso();
      if (res?.success && res.data) {
        setConfig(res.data);
        showToast.success(res.message || 'SSO enabled');
      } else {
        showToast.error(res?.message || 'Could not enable SSO');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Could not enable SSO'));
    } finally {
      setEnabling(false);
    }
  };

  const handleDisable = async () => {
    if (!window.confirm('Disable company SSO? Employees will need another allowed sign-in method.')) {
      return;
    }
    setDisabling(true);
    try {
      const res = await corporateService.disableSso();
      if (res?.success && res.data) {
        setConfig(res.data);
        showToast.success(res.message || 'SSO disabled');
      } else {
        showToast.error(res?.message || 'Could not disable SSO');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Could not disable SSO'));
    } finally {
      setDisabling(false);
    }
  };

  const handleAddDomain = async (e) => {
    e.preventDefault();
    const domain = newDomain.trim().toLowerCase();
    if (!domain) return;
    setAddingDomain(true);
    try {
      const res = await corporateService.addSsoDomain(domain);
      if (res?.success) {
        showToast.success(res.message || 'Domain added');
        setNewDomain('');
        await loadConfig();
      } else {
        showToast.error(res?.message || 'Could not add domain');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Could not add domain'));
    } finally {
      setAddingDomain(false);
    }
  };

  const handleVerifyDomain = async (domainId) => {
    setDomainBusyId(domainId);
    try {
      const res = await corporateService.verifySsoDomain(domainId);
      if (res?.success) {
        showToast.success(res.message || 'Domain verified');
        await loadConfig();
      } else {
        showToast.error(res?.message || 'Domain verification failed');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Domain verification failed'));
    } finally {
      setDomainBusyId(null);
    }
  };

  const handleRemoveDomain = async (domainId, domainName) => {
    if (!window.confirm(`Remove domain ${domainName}?`)) return;
    setDomainBusyId(domainId);
    try {
      const res = await corporateService.removeSsoDomain(domainId);
      if (res?.success) {
        showToast.success(res.message || 'Domain removed');
        await loadConfig();
      } else {
        showToast.error(res?.message || 'Could not remove domain');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Could not remove domain'));
    } finally {
      setDomainBusyId(null);
    }
  };

  const handleSaveSlug = async (e) => {
    e.preventDefault();
    const next = slug.trim().toLowerCase();
    if (!next) {
      showToast.error('Company slug is required');
      return;
    }
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(next)) {
      showToast.error('Slug must be lowercase letters, numbers, and hyphens (no leading/trailing hyphen)');
      return;
    }
    setSavingSlug(true);
    try {
      const res = await corporateService.updateCompany({ slug: next });
      if (res?.success) {
        showToast.success(res.message || 'Company slug updated');
        if (refreshCompanyDetails) await refreshCompanyDetails();
        if (res.data?.slug) setSlug(res.data.slug);
      } else {
        showToast.error(res?.message || 'Could not update slug');
      }
    } catch (err) {
      showToast.error(handleApiError(err, 'Could not update slug'));
    } finally {
      setSavingSlug(false);
    }
  };

  const loadAudit = async () => {
    setShowAudit(true);
    setAuditLoading(true);
    try {
      const res = await corporateService.getSsoAudit(50);
      if (res?.success && Array.isArray(res.data)) {
        setAuditEvents(res.data);
      } else {
        setAuditEvents([]);
        showToast.error(res?.message || 'Could not load audit events');
      }
    } catch (err) {
      setAuditEvents([]);
      showToast.error(handleApiError(err, 'Could not load audit events'));
    } finally {
      setAuditLoading(false);
    }
  };

  if (!isCompanyAdmin) {
    return (
      <div style={cardStyle}>
        <h2 style={{ color: 'var(--color-text-primary)', fontSize: '1.1rem', margin: '0 0 0.75rem 0' }}>
          Company SSO
        </h2>
        <p style={{ color: 'var(--color-text-secondary)', margin: 0 }}>
          Only company admins can configure enterprise SSO. Contact your company administrator.
        </p>
      </div>
    );
  }

  if (loading) {
    return (
      <div style={{ padding: '3rem', textAlign: 'center' }}>
        <div className="spinner" />
      </div>
    );
  }

  const domains = config?.domains || [];
  const forceLocked = !!config?.forceDisabledByPlatform;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }} data-testid="company-sso-settings">
      {/* Status */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <h2 style={{ color: 'var(--color-text-primary)', fontSize: '1.1rem', margin: '0 0 0.35rem 0' }}>
              Company SSO
            </h2>
            <p style={{ color: 'var(--color-text-secondary)', margin: 0, fontSize: '0.9rem' }}>
              Configure your company identity provider (OIDC). Employees sign in with company SSO from{' '}
              <code style={{ color: 'var(--color-accent-light)' }}>/corporate/login</code>.
            </p>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', alignItems: 'center' }}>
            <StatusBadge config={config} />
            <span style={{ ...badgeBase, background: 'var(--color-row-elevated)', color: 'var(--color-text-secondary)' }}>
              Protocol: OIDC
            </span>
          </div>
        </div>

        {forceLocked && (
          <div
            role="alert"
            style={{
              marginTop: '1rem',
              padding: '0.85rem 1rem',
              borderRadius: '8px',
              border: '1px solid var(--color-error)',
              background: 'rgba(239,68,68,0.08)',
              color: 'var(--color-text-primary)',
            }}
          >
            <strong style={{ color: 'var(--color-error)' }}>Platform force-disable is active.</strong>
            <div style={{ marginTop: '0.35rem', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
              SSO cannot be re-enabled by company admins until ParkEase platform support clears the lock.
              You may still update configuration and disable SSO, but Enable is blocked.
            </div>
          </div>
        )}

        <div
          style={{
            marginTop: '1rem',
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
            gap: '0.75rem',
            fontSize: '0.85rem',
            color: 'var(--color-text-secondary)',
          }}
        >
          <div>
            <div style={{ fontWeight: 600, color: 'var(--color-text-primary)' }}>Last test</div>
            <div>
              {config?.lastTestedAt
                ? new Date(config.lastTestedAt).toLocaleString()
                : 'Never'}
            </div>
            <div>
              Result:{' '}
              <strong style={{ color: lastTestOk ? 'var(--color-success)' : 'var(--color-text-primary)' }}>
                {config?.lastTestResult || '—'}
              </strong>
            </div>
          </div>
          <div>
            <div style={{ fontWeight: 600, color: 'var(--color-text-primary)' }}>Client secret</div>
            <div>
              {config?.secretUnreadable
                ? 'Stored secret unreadable — re-enter'
                : config?.hasClientSecret
                  ? 'Configured'
                  : 'Not set'}
            </div>
          </div>
          <div>
            <div style={{ fontWeight: 600, color: 'var(--color-text-primary)' }}>Verified domains</div>
            <div>
              {verifiedDomainCount} / {domains.length}
            </div>
          </div>
        </div>
      </div>

      {/* Company slug */}
      <div style={cardStyle}>
        <h3 style={{ color: 'var(--color-text-primary)', fontSize: '1rem', margin: '0 0 0.75rem 0' }}>
          Company login slug
        </h3>
        <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem', margin: '0 0 1rem 0' }}>
          Used for direct SSO entry links: <code>/corporate/login?company=&#123;slug&#125;</code>
        </p>
        <form onSubmit={handleSaveSlug} style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'flex-end' }}>
          <div style={{ flex: '1 1 220px' }}>
            <label style={labelStyle}>Slug</label>
            <input
              value={slug}
              onChange={(e) => setSlug(e.target.value.toLowerCase())}
              placeholder="acme-corp"
              maxLength={64}
              pattern="[a-z0-9]+(-[a-z0-9]+)*"
              style={fieldStyle}
              data-testid="company-sso-slug"
            />
          </div>
          <button type="submit" className="btn btn-secondary" disabled={savingSlug}>
            {savingSlug ? 'Saving…' : 'Save slug'}
          </button>
          {loginUrl && (
            <button type="button" className="btn btn-secondary" onClick={() => copyText(loginUrl, 'Login link copied')}>
              Copy login link
            </button>
          )}
        </form>
      </div>

      {/* IdP details */}
      <form onSubmit={handleSaveConfig} style={cardStyle}>
        <h3 style={{ color: 'var(--color-text-primary)', fontSize: '1rem', margin: '0 0 1rem 0' }}>
          Identity provider (OIDC)
        </h3>
        <p style={{ color: 'var(--color-text-muted)', fontSize: '0.8rem', margin: '0 0 1rem 0' }}>
          SAML is not available in this release.
        </p>

        <div className="form-group" style={{ marginBottom: '1rem' }}>
          <label style={labelStyle}>Authority / Tenant URL *</label>
          <input
            required
            type="url"
            placeholder="https://login.microsoftonline.com/{tenant-id}/v2.0"
            value={form.authority}
            onChange={(e) => setForm({ ...form, authority: e.target.value })}
            style={fieldStyle}
            data-testid="company-sso-authority"
          />
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
          <div className="form-group">
            <label style={labelStyle}>Client ID *</label>
            <input
              required
              value={form.clientId}
              onChange={(e) => setForm({ ...form, clientId: e.target.value })}
              style={fieldStyle}
              data-testid="company-sso-client-id"
            />
          </div>
          <div className="form-group">
            <label style={labelStyle}>
              Client secret {config?.hasClientSecret ? '(leave blank to keep)' : '*'}
            </label>
            <input
              type="password"
              autoComplete="new-password"
              placeholder={config?.hasClientSecret ? '••••••••' : 'Enter client secret'}
              value={form.clientSecret}
              onChange={(e) => setForm({ ...form, clientSecret: e.target.value })}
              style={fieldStyle}
              data-testid="company-sso-client-secret"
            />
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem', marginTop: '1rem' }}>
          <div className="form-group">
            <label style={labelStyle}>Metadata URL (optional)</label>
            <input
              type="url"
              placeholder="Defaults to {authority}/.well-known/openid-configuration"
              value={form.metadataUrl}
              onChange={(e) => setForm({ ...form, metadataUrl: e.target.value })}
              style={fieldStyle}
            />
          </div>
          <div className="form-group">
            <label style={labelStyle}>Token endpoint auth</label>
            <select
              value={form.tokenEndpointAuthMethod}
              onChange={(e) => setForm({ ...form, tokenEndpointAuthMethod: e.target.value })}
              style={fieldStyle}
            >
              <option value="client_secret_post">client_secret_post</option>
              <option value="client_secret_basic">client_secret_basic</option>
            </select>
          </div>
        </div>

        <div className="form-group" style={{ marginTop: '1rem' }}>
          <label style={labelStyle}>Redirect URI (register in your IdP)</label>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <input
              readOnly
              value={config?.redirectUri || ''}
              style={{ ...fieldStyle, flex: 1, color: 'var(--color-text-muted)' }}
              data-testid="company-sso-redirect-uri"
            />
            <button
              type="button"
              className="btn btn-secondary"
              disabled={!config?.redirectUri}
              onClick={() => copyText(config.redirectUri, 'Redirect URI copied')}
            >
              Copy
            </button>
          </div>
          <small style={{ color: 'var(--color-text-muted)', display: 'block', marginTop: '6px' }}>
            Entra: App registration → Authentication → Web redirect URIs. Use this exact URL.
          </small>
        </div>

        {/* Policies */}
        <h3 style={{ color: 'var(--color-text-primary)', fontSize: '1rem', margin: '1.5rem 0 0.75rem 0' }}>
          Policies
        </h3>

        <label style={{ display: 'flex', gap: '0.6rem', alignItems: 'flex-start', marginBottom: '0.75rem', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={form.passwordLoginAllowed}
            onChange={(e) => {
              const allowed = e.target.checked;
              setForm({ ...form, passwordLoginAllowed: allowed });
              if (allowed) setConfirmSsoOnly(false);
            }}
            style={{ marginTop: '3px' }}
            data-testid="company-sso-password-allowed"
          />
          <span>
            <strong style={{ color: 'var(--color-text-primary)' }}>Allow password login (fallback)</strong>
            <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              Recommended during rollout. Keep enabled until SSO is proven.
            </div>
          </span>
        </label>

        {!form.passwordLoginAllowed && (
          <div
            style={{
              margin: '0 0 0.85rem 0',
              padding: '0.85rem 1rem',
              borderRadius: '8px',
              border: '1px solid var(--color-warning)',
              background: 'rgba(234,179,8,0.08)',
            }}
          >
            <strong style={{ color: 'var(--color-warning)' }}>SSO-only mode</strong>
            <p style={{ margin: '0.4rem 0', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              Disables password sign-in for <em>everyone</em> in this company, including company admins.
              Platform break-glass only if the IdP is down.
            </p>
            <ul style={{ margin: '0 0 0.6rem 1.1rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              <li>Verified domains: {verifiedDomainCount >= 1 ? '✓' : '✗ required'}</li>
              <li>Last connection test: {lastTestOk ? '✓' : '✗ success required'}</li>
            </ul>
            <label style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-start', cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={confirmSsoOnly}
                onChange={(e) => setConfirmSsoOnly(e.target.checked)}
                disabled={!ssoOnlyReady && config?.passwordLoginAllowed !== false}
                style={{ marginTop: '3px' }}
                data-testid="company-sso-confirm-sso-only"
              />
              <span style={{ color: 'var(--color-text-primary)', fontSize: '0.9rem' }}>
                I understand password login will be blocked for all company members
              </span>
            </label>
          </div>
        )}

        <label style={{ display: 'flex', gap: '0.6rem', alignItems: 'flex-start', marginBottom: '0.75rem', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={form.forceAuthn}
            onChange={(e) => setForm({ ...form, forceAuthn: e.target.checked })}
            style={{ marginTop: '3px' }}
          />
          <span>
            <strong style={{ color: 'var(--color-text-primary)' }}>Force re-authentication at IdP</strong>
            <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              Request prompt=login so users re-enter credentials at the identity provider.
            </div>
          </span>
        </label>

        <label style={{ display: 'flex', gap: '0.6rem', alignItems: 'flex-start', marginBottom: '0.75rem', opacity: 0.85 }}>
          <input type="checkbox" checked={form.autoAcceptInvitationsOnSso} readOnly disabled style={{ marginTop: '3px' }} />
          <span>
            <strong style={{ color: 'var(--color-text-primary)' }}>Auto-accept invitations on SSO</strong>
            <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              Always on for MVP: a valid pending invite grants membership on first successful SSO.
            </div>
          </span>
        </label>

        <button
          type="button"
          className="btn btn-secondary"
          style={{ marginBottom: '0.75rem', fontSize: '0.85rem' }}
          onClick={() => setShowAdvanced((v) => !v)}
        >
          {showAdvanced ? 'Hide' : 'Show'} advanced attribute mapping
        </button>

        {showAdvanced && (
          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label style={labelStyle}>Attribute mapping (JSON, optional)</label>
            <textarea
              rows={5}
              value={form.attributeMappingJson}
              onChange={(e) => setForm({ ...form, attributeMappingJson: e.target.value })}
              placeholder='{"email":"email","name":"name"}'
              style={{ ...fieldStyle, fontFamily: 'ui-monospace, monospace', resize: 'vertical' }}
            />
          </div>
        )}

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.65rem', marginTop: '0.5rem' }}>
          <button type="submit" className="btn btn-primary" disabled={saving} data-testid="company-sso-save">
            {saving ? 'Saving…' : 'Save configuration'}
          </button>
          <button type="button" className="btn btn-secondary" onClick={handleTest} disabled={testing}>
            {testing ? 'Testing…' : 'Test connection'}
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleEnable}
            disabled={enabling || forceLocked || config?.isEnabled}
            title={forceLocked ? 'Blocked by platform force-disable' : undefined}
            data-testid="company-sso-enable"
          >
            {enabling ? 'Enabling…' : 'Enable SSO'}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={handleDisable}
            disabled={disabling || !config?.isEnabled}
            data-testid="company-sso-disable"
          >
            {disabling ? 'Disabling…' : 'Disable SSO'}
          </button>
          <button type="button" className="btn btn-secondary" onClick={loadAudit}>
            View audit
          </button>
        </div>
      </form>

      {/* Domains */}
      <div style={cardStyle}>
        <h3 style={{ color: 'var(--color-text-primary)', fontSize: '1rem', margin: '0 0 0.5rem 0' }}>
          Email domains
        </h3>
        <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem', margin: '0 0 1rem 0' }}>
          At least one verified domain is required before SSO can be enabled. Add a DNS TXT record, then verify.
        </p>

        <form onSubmit={handleAddDomain} style={{ display: 'flex', flexWrap: 'wrap', gap: '0.65rem', marginBottom: '1rem' }}>
          <input
            value={newDomain}
            onChange={(e) => setNewDomain(e.target.value)}
            placeholder="acme.com"
            style={{ ...fieldStyle, flex: '1 1 200px' }}
            data-testid="company-sso-domain-input"
          />
          <button type="submit" className="btn btn-secondary" disabled={addingDomain || !newDomain.trim()}>
            {addingDomain ? 'Adding…' : 'Add domain'}
          </button>
        </form>

        {domains.length === 0 ? (
          <p style={{ color: 'var(--color-text-muted)', margin: 0 }}>No domains configured yet.</p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            {domains.map((d) => (
              <div
                key={d.id}
                style={{
                  padding: '0.9rem 1rem',
                  borderRadius: '8px',
                  background: 'var(--color-row-elevated)',
                  border: '1px solid var(--color-border)',
                }}
              >
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div>
                    <strong style={{ color: 'var(--color-text-primary)' }}>{d.domain}</strong>
                    <span
                      style={{
                        marginLeft: '0.5rem',
                        ...badgeBase,
                        background: d.isVerified ? 'rgba(34,197,94,0.15)' : 'rgba(234,179,8,0.15)',
                        color: d.isVerified ? 'var(--color-success)' : 'var(--color-warning)',
                      }}
                    >
                      {d.isVerified ? 'Verified' : 'Unverified'}
                    </span>
                  </div>
                  <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap' }}>
                    {!d.isVerified && (
                      <button
                        type="button"
                        className="btn btn-primary"
                        style={{ padding: '0.35rem 0.7rem', fontSize: '0.8rem' }}
                        disabled={domainBusyId === d.id}
                        onClick={() => handleVerifyDomain(d.id)}
                      >
                        {domainBusyId === d.id ? 'Checking…' : 'Verify DNS'}
                      </button>
                    )}
                    <button
                      type="button"
                      className="btn btn-secondary"
                      style={{ padding: '0.35rem 0.7rem', fontSize: '0.8rem' }}
                      disabled={domainBusyId === d.id}
                      onClick={() => handleRemoveDomain(d.id, d.domain)}
                    >
                      Remove
                    </button>
                  </div>
                </div>
                {!d.isVerified && (
                  <div style={{ marginTop: '0.65rem', fontSize: '0.82rem', color: 'var(--color-text-secondary)' }}>
                    <div>
                      Host: <code style={{ color: 'var(--color-accent-light)' }}>{d.preferredTxtHost}</code>
                      <button
                        type="button"
                        className="btn btn-secondary"
                        style={{ marginLeft: '0.4rem', padding: '0.15rem 0.45rem', fontSize: '0.75rem' }}
                        onClick={() => copyText(d.preferredTxtHost, 'TXT host copied')}
                      >
                        Copy
                      </button>
                    </div>
                    <div style={{ marginTop: '0.35rem' }}>
                      Value: <code style={{ color: 'var(--color-accent-light)', wordBreak: 'break-all' }}>{d.expectedTxtValue}</code>
                      <button
                        type="button"
                        className="btn btn-secondary"
                        style={{ marginLeft: '0.4rem', padding: '0.15rem 0.45rem', fontSize: '0.75rem' }}
                        onClick={() => copyText(d.expectedTxtValue, 'TXT value copied')}
                      >
                        Copy
                      </button>
                    </div>
                    <div style={{ marginTop: '0.35rem', color: 'var(--color-text-muted)' }}>
                      Apex <code>@</code> / root TXT is also accepted as fallback.
                    </div>
                  </div>
                )}
                {d.isVerified && d.verifiedAt && (
                  <div style={{ marginTop: '0.4rem', fontSize: '0.8rem', color: 'var(--color-text-muted)' }}>
                    Verified {new Date(d.verifiedAt).toLocaleString()}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Audit drawer */}
      {showAudit && (
        <div style={cardStyle} data-testid="company-sso-audit">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
            <h3 style={{ color: 'var(--color-text-primary)', fontSize: '1rem', margin: 0 }}>Recent SSO audit</h3>
            <button type="button" className="btn btn-secondary" style={{ fontSize: '0.8rem' }} onClick={() => setShowAudit(false)}>
              Close
            </button>
          </div>
          {auditLoading ? (
            <div style={{ padding: '1.5rem', textAlign: 'center' }}><div className="spinner" /></div>
          ) : auditEvents.length === 0 ? (
            <p style={{ color: 'var(--color-text-muted)', margin: 0 }}>No audit events yet.</p>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                <thead>
                  <tr style={{ color: 'var(--color-text-secondary)', textAlign: 'left' }}>
                    <th style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)' }}>When</th>
                    <th style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)' }}>Action</th>
                    <th style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)' }}>Outcome</th>
                    <th style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)' }}>Error</th>
                  </tr>
                </thead>
                <tbody>
                  {auditEvents.map((ev) => (
                    <tr key={ev.id}>
                      <td style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)', color: 'var(--color-text-secondary)' }}>
                        {ev.createdAt ? new Date(ev.createdAt).toLocaleString() : '—'}
                      </td>
                      <td style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)', color: 'var(--color-text-primary)' }}>
                        {ev.action}
                      </td>
                      <td style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)', color: 'var(--color-text-primary)' }}>
                        {ev.outcome}
                      </td>
                      <td style={{ padding: '0.45rem', borderBottom: '1px solid var(--color-border)', color: 'var(--color-text-muted)' }}>
                        {ev.errorCode || '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
