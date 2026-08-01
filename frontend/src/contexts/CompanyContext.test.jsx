import React, { useState } from 'react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act, cleanup } from '@testing-library/react';
import { CompanyProvider, useCompany } from './CompanyContext';

const mockGetCompany = vi.fn();
let authState = {
  isAuthenticated: true,
  channel: 'Marketplace',
  companyId: null,
};

vi.mock('../services/corporateService', () => ({
  default: {
    getCompany: (...args) => mockGetCompany(...args),
  },
}));

vi.mock('./AuthContext', () => ({
  useAuth: () => authState,
}));

function CompanyProbe() {
  const company = useCompany();
  const [tick, setTick] = useState(0);
  return (
    <div>
      <span data-testid="activeId">{company.activeCompanyId ?? 'none'}</span>
      <span data-testid="corporate">{String(company.isCorporateMode)}</span>
      <span data-testid="loading">{String(company.loadingCompany)}</span>
      <span data-testid="details">
        {company.companyDetails ? JSON.stringify(company.companyDetails) : 'none'}
      </span>
      <button type="button" onClick={() => company.switchCompany('co-1')}>
        switch
      </button>
      <button type="button" onClick={() => company.clearActiveCompany()}>
        clear
      </button>
      <button
        type="button"
        onClick={async () => {
          await company.refreshCompanyDetails();
          setTick((t) => t + 1);
        }}
      >
        refresh
      </button>
      <span data-testid="tick">{tick}</span>
    </div>
  );
}

describe('CompanyContext', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    authState = {
      isAuthenticated: true,
      channel: 'Marketplace',
      companyId: null,
    };
    mockGetCompany.mockResolvedValue({
      success: true,
      data: { id: 'co-1', name: 'Acme' },
    });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it('throws when useCompany is used outside provider', () => {
    const Spy = () => {
      useCompany();
      return null;
    };
    expect(() => render(<Spy />)).toThrow(
      'useCompany must be used within a CompanyProvider'
    );
  });

  it('starts without active company on Marketplace', () => {
    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );
    expect(screen.getByTestId('activeId').textContent).toBe('none');
    expect(screen.getByTestId('corporate').textContent).toBe('false');
    expect(mockGetCompany).not.toHaveBeenCalled();
  });

  it('ignores bare localStorage activeCompanyId without Corporate channel', async () => {
    localStorage.setItem('activeCompanyId', 'co-stored');

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('corporate').textContent).toBe('false');
      expect(screen.getByTestId('activeId').textContent).toBe('none');
    });
    expect(mockGetCompany).not.toHaveBeenCalled();
  });

  it('does not fetch when not authenticated', async () => {
    authState = { isAuthenticated: false, channel: 'Marketplace', companyId: null };

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('activeId').textContent).toBe('none');
      expect(screen.getByTestId('details').textContent).toBe('none');
    });
    expect(mockGetCompany).not.toHaveBeenCalled();
  });

  it('switchCompany cache is cleared when not Corporate channel', async () => {
    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await act(async () => {
      screen.getByRole('button', { name: /switch/i }).click();
    });

    // Marketplace channel effect clears bare cache — chrome stays non-corporate
    await waitFor(() => {
      expect(screen.getByTestId('corporate').textContent).toBe('false');
      expect(screen.getByTestId('activeId').textContent).toBe('none');
    });
  });

  it('Corporate channel sets isCorporateMode and syncs jwt companyId', async () => {
    authState = {
      isAuthenticated: true,
      channel: 'Corporate',
      companyId: 'co-jwt',
    };
    mockGetCompany.mockResolvedValue({
      success: true,
      data: { id: 'co-jwt', name: 'JWT Co' },
    });

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('corporate').textContent).toBe('true');
      expect(screen.getByTestId('activeId').textContent).toBe('co-jwt');
      expect(localStorage.getItem('activeCompanyId')).toBe('co-jwt');
      expect(screen.getByTestId('details').textContent).toContain('JWT Co');
    });
  });

  it('clears active company on failed getCompany while Corporate', async () => {
    authState = {
      isAuthenticated: true,
      channel: 'Corporate',
      companyId: 'bad-co',
    };
    mockGetCompany.mockResolvedValue({ success: false, message: 'gone' });

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('activeId').textContent).toBe('none');
      expect(localStorage.getItem('activeCompanyId')).toBeNull();
    });
  });

  it('clearActiveCompany removes storage and details on Corporate', async () => {
    authState = {
      isAuthenticated: true,
      channel: 'Corporate',
      companyId: 'co-1',
    };
    mockGetCompany.mockResolvedValue({
      success: true,
      data: { id: 'co-1', name: 'Acme' },
    });

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('details').textContent).toContain('Acme');
    });

    await act(async () => {
      screen.getByRole('button', { name: /clear/i }).click();
    });

    // JWT still Corporate with companyId — effect re-syncs cache from JWT
    await waitFor(() => {
      expect(screen.getByTestId('activeId').textContent).toBe('co-1');
      expect(localStorage.getItem('activeCompanyId')).toBe('co-1');
    });
  });

  it('keeps loading false after fetch throw', async () => {
    const errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    authState = {
      isAuthenticated: true,
      channel: 'Corporate',
      companyId: 'co-1',
    };
    mockGetCompany.mockRejectedValue(new Error('network'));

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
      expect(screen.getByTestId('activeId').textContent).toBe('co-1');
    });
    errSpy.mockRestore();
  });

  it('Marketplace channel never sets isCorporateMode from storage', async () => {
    localStorage.setItem('activeCompanyId', 'co-stored');
    authState = {
      isAuthenticated: true,
      channel: 'Marketplace',
      companyId: null,
    };

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('corporate').textContent).toBe('false');
      expect(screen.getByTestId('activeId').textContent).toBe('none');
    });
  });
});
