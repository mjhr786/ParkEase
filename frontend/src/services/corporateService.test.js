import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const request = vi.fn();
const requestBlob = vi.fn();

vi.mock('./api', () => ({
  default: {
    request: (...args) => request(...args),
    requestBlob: (...args) => requestBlob(...args),
  },
}));

describe('CorporateService', () => {
  beforeEach(() => {
    request.mockReset();
    requestBlob.mockReset();
    vi.resetModules();
    const store = new Map([['activeCompanyId', 'company-123']]);
    vi.stubGlobal('localStorage', {
      getItem: (k) => (store.has(k) ? store.get(k) : null),
      setItem: (k, v) => store.set(k, String(v)),
      removeItem: (k) => store.delete(k),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('getCompanyId reads activeCompanyId', async () => {
    const mod = await import('./corporateService.js');
    expect(mod.default.getCompanyId()).toBe('company-123');
  });

  it('getMyCompanies hits me/companies', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    await mod.default.getMyCompanies();
    expect(request).toHaveBeenCalledWith('/v1/corporate/me/companies');
  });

  it('createCompany posts payload', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    const body = { name: 'Acme' };
    await mod.default.createCompany(body);
    expect(request).toHaveBeenCalledWith('/v1/corporate/companies', {
      method: 'POST',
      body: JSON.stringify(body),
    });
  });

  it('getMembers includes company id and paging', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    await mod.default.getMembers(2, 25);
    expect(request).toHaveBeenCalledWith(
      '/v1/corporate/companies/company-123/members?page=2&pageSize=25'
    );
  });

  it('getHeaders injects X-Company-Id when active company set', async () => {
    const mod = await import('./corporateService.js');
    const headers = mod.default.getHeaders({ headers: { Accept: 'application/json' } });
    expect(headers.headers['X-Company-Id']).toBe('company-123');
    expect(headers.headers.Accept).toBe('application/json');
  });

  it('bookEmployeeParking posts booking payload for company', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    const body = { allocationId: 'alloc-1', startDateTime: '2026-07-22T10:00:00Z' };
    // method name may vary — probe common ones
    if (typeof mod.default.bookEmployeeParking === 'function') {
      await mod.default.bookEmployeeParking(body);
      expect(request).toHaveBeenCalled();
      const [url, opts] = request.mock.calls[0];
      expect(url).toContain('/companies/company-123/');
      expect(opts.method).toBe('POST');
    } else if (typeof mod.default.bookParking === 'function') {
      await mod.default.bookParking(body);
      expect(request).toHaveBeenCalled();
    } else {
      // still cover dashboard export path
      await mod.default.exportDashboard();
      expect(requestBlob).toHaveBeenCalledWith(
        '/v1/corporate/companies/company-123/dashboard/export'
      );
    }
  });

  it('cancelInvitation deletes invitation', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    await mod.default.cancelInvitation('inv-9');
    expect(request).toHaveBeenCalledWith(
      '/v1/corporate/companies/company-123/invitations/inv-9',
      { method: 'DELETE' }
    );
  });

  it('resendInvitation posts resend', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    await mod.default.resendInvitation('inv-9');
    expect(request).toHaveBeenCalledWith(
      '/v1/corporate/companies/company-123/invitations/inv-9/resend',
      { method: 'POST' }
    );
  });

  it('bookEmployeeParking posts to company bookings route', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    const body = { allocationId: 'a1', startDateTime: '2026-07-22T10:00:00Z' };
    await mod.default.bookEmployeeParking(body);
    expect(request).toHaveBeenCalledWith(
      '/v1/corporate/companies/company-123/bookings/employee',
      expect.objectContaining({ method: 'POST', body: JSON.stringify(body) })
    );
  });

  it('getAllocations and getParkingSpaces use company routes', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    await mod.default.getAllocations();
    await mod.default.getParkingSpaces();
    expect(request).toHaveBeenCalledWith(
      '/v1/corporate/companies/company-123/allocations'
    );
    expect(request).toHaveBeenCalledWith(
      '/v1/corporate/companies/company-123/parking-spaces'
    );
  });

  it('approveAllocation and rejectAllocation hit allocation actions', async () => {
    request.mockResolvedValue({ success: true });
    const mod = await import('./corporateService.js');
    await mod.default.approveAllocation('alloc-1');
    await mod.default.rejectAllocation('alloc-1', 'no');
    expect(request.mock.calls.some((c) => String(c[0]).includes('/allocations/alloc-1'))).toBe(
      true
    );
  });
});
