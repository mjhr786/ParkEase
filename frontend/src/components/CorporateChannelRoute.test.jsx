import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import CorporateChannelRoute from './CorporateChannelRoute';

let authState;

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authState,
}));

function renderAt(path, ui) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/corporate/dashboard" element={ui} />
        <Route path="/corporate/create-company" element={ui} />
        <Route path="/corporate/login" element={<div>Corporate Login</div>} />
        <Route path="/login" element={<div>Marketplace Login</div>} />
        <Route path="/corporate/create-company-page" element={<div>Create Company Page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('CorporateChannelRoute', () => {
  beforeEach(() => {
    authState = {
      isAuthenticated: true,
      loading: false,
      channel: 'Marketplace',
      companyId: null,
      isBootstrap: false,
    };
  });

  afterEach(() => {
    cleanup();
  });

  it('denies Marketplace channel even when authenticated (no soft mode)', () => {
    renderAt(
      '/corporate/dashboard',
      <CorporateChannelRoute>
        <div>Corp Dash</div>
      </CorporateChannelRoute>
    );
    expect(screen.getByText('Corporate Login')).toBeInTheDocument();
    expect(screen.queryByText('Corp Dash')).not.toBeInTheDocument();
  });

  it('redirects unauthenticated to corporate login', () => {
    authState = { ...authState, isAuthenticated: false };
    renderAt(
      '/corporate/dashboard',
      <CorporateChannelRoute>
        <div>Corp Dash</div>
      </CorporateChannelRoute>
    );
    expect(screen.getByText('Corporate Login')).toBeInTheDocument();
    expect(screen.queryByText('Marketplace Login')).not.toBeInTheDocument();
  });

  it('deep link corporate without Corporate channel → corporate login', () => {
    authState = {
      isAuthenticated: true,
      loading: false,
      channel: 'Marketplace',
      companyId: null,
      isBootstrap: false,
    };
    renderAt(
      '/corporate/dashboard',
      <CorporateChannelRoute>
        <div>Corp Dash</div>
      </CorporateChannelRoute>
    );
    expect(screen.getByText('Corporate Login')).toBeInTheDocument();
    expect(screen.queryByText('Corp Dash')).not.toBeInTheDocument();
  });

  it('allows Corporate channel with companyId', () => {
    authState = {
      isAuthenticated: true,
      loading: false,
      channel: 'Corporate',
      companyId: 'co-1',
      isBootstrap: false,
    };
    renderAt(
      '/corporate/dashboard',
      <CorporateChannelRoute>
        <div>Corp Dash</div>
      </CorporateChannelRoute>
    );
    expect(screen.getByText('Corp Dash')).toBeInTheDocument();
  });

  it('redirects Corporate bootstrap without company to create-company', () => {
    authState = {
      isAuthenticated: true,
      loading: false,
      channel: 'Corporate',
      companyId: null,
      isBootstrap: true,
    };
    render(
      <MemoryRouter initialEntries={['/corporate/dashboard']}>
        <Routes>
          <Route
            path="/corporate/dashboard"
            element={
              <CorporateChannelRoute>
                <div>Corp Dash</div>
              </CorporateChannelRoute>
            }
          />
          <Route path="/corporate/create-company" element={<div>Create Company Page</div>} />
          <Route path="/corporate/login" element={<div>Corporate Login</div>} />
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByText('Create Company Page')).toBeInTheDocument();
  });

  it('allows bootstrap path when allowBootstrap is set', () => {
    authState = {
      isAuthenticated: true,
      loading: false,
      channel: 'Corporate',
      companyId: null,
      isBootstrap: true,
    };
    render(
      <MemoryRouter initialEntries={['/corporate/create-company']}>
        <Routes>
          <Route
            path="/corporate/create-company"
            element={
              <CorporateChannelRoute allowBootstrap>
                <div>Create Form</div>
              </CorporateChannelRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByText('Create Form')).toBeInTheDocument();
  });
});
