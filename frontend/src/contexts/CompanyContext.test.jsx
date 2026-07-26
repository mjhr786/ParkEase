import React, { useState } from 'react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act, cleanup } from '@testing-library/react';
import { CompanyProvider, useCompany } from './CompanyContext';

const mockGetCompany = vi.fn();
let authState = { isAuthenticated: true };

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
    authState = { isAuthenticated: true };
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

  it('starts without active company', () => {
    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );
    expect(screen.getByTestId('activeId').textContent).toBe('none');
    expect(screen.getByTestId('corporate').textContent).toBe('false');
    expect(mockGetCompany).not.toHaveBeenCalled();
  });

  it('hydrates activeCompanyId from localStorage and fetches details when authenticated', async () => {
    localStorage.setItem('activeCompanyId', 'co-stored');
    mockGetCompany.mockResolvedValue({
      success: true,
      data: { id: 'co-stored', name: 'Stored Co' },
    });

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('activeId').textContent).toBe('co-stored');
      expect(screen.getByTestId('details').textContent).toContain('Stored Co');
      expect(screen.getByTestId('corporate').textContent).toBe('true');
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });
    expect(mockGetCompany).toHaveBeenCalled();
  });

  it('does not fetch when not authenticated even with stored id', async () => {
    authState = { isAuthenticated: false };
    localStorage.setItem('activeCompanyId', 'co-1');

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('activeId').textContent).toBe('co-1');
      expect(screen.getByTestId('details').textContent).toBe('none');
    });
    expect(mockGetCompany).not.toHaveBeenCalled();
  });

  it('switchCompany persists id and loads details', async () => {
    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await act(async () => {
      screen.getByRole('button', { name: /switch/i }).click();
    });

    await waitFor(() => {
      expect(localStorage.getItem('activeCompanyId')).toBe('co-1');
      expect(screen.getByTestId('activeId').textContent).toBe('co-1');
      expect(screen.getByTestId('details').textContent).toContain('Acme');
    });
  });

  it('clears active company on failed getCompany', async () => {
    localStorage.setItem('activeCompanyId', 'bad-co');
    mockGetCompany.mockResolvedValue({ success: false, message: 'gone' });

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('activeId').textContent).toBe('none');
      expect(localStorage.getItem('activeCompanyId')).toBeNull();
      expect(screen.getByTestId('corporate').textContent).toBe('false');
    });
  });

  it('clearActiveCompany removes storage and details', async () => {
    localStorage.setItem('activeCompanyId', 'co-1');
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

    expect(screen.getByTestId('activeId').textContent).toBe('none');
    expect(localStorage.getItem('activeCompanyId')).toBeNull();
    expect(screen.getByTestId('details').textContent).toBe('none');
  });

  it('keeps company on fetch throw and stops loading', async () => {
    const errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    localStorage.setItem('activeCompanyId', 'co-1');
    mockGetCompany.mockRejectedValue(new Error('network'));

    render(
      <CompanyProvider>
        <CompanyProbe />
      </CompanyProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
      expect(screen.getByTestId('activeId').textContent).toBe('co-1');
      expect(screen.getByTestId('details').textContent).toBe('none');
    });
    errSpy.mockRestore();
  });
});
