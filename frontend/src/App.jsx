import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate, useLocation, useSearchParams } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ChatProvider, useChatContext } from './contexts/ChatContext';
import { NotificationProvider, useNotificationContext } from './context/NotificationContext';
import { CompanyProvider, useCompany } from './contexts/CompanyContext';
import NotificationDropdown from './components/NotificationDropdown';
import CompanySwitcher from './components/CompanySwitcher';
import toast, { Toaster } from 'react-hot-toast';
import React, { Suspense } from 'react';
import './index.css';
import api from './services/api';

// Lazy load pages
const Home = React.lazy(() => import('./pages/Home'));
const Login = React.lazy(() => import('./pages/Login'));
const Register = React.lazy(() => import('./pages/Register'));
const Search = React.lazy(() => import('./pages/Search'));
const ParkingDetails = React.lazy(() => import('./pages/ParkingDetails'));
const Dashboard = React.lazy(() => import('./pages/Dashboard'));
const MyBookings = React.lazy(() => import('./pages/MyBookings'));
const VendorListings = React.lazy(() => import('./pages/VendorListings'));
const VendorBookings = React.lazy(() => import('./pages/VendorBookings'));
const Chat = React.lazy(() => import('./pages/Chat'));
const MyFavorites = React.lazy(() => import('./pages/MyFavorites'));
const MyGarage = React.lazy(() => import('./pages/MyGarage'));
const Profile = React.lazy(() => import('./pages/Profile'));

const CorporateDashboard = React.lazy(() => import('./pages/Corporate/CorporateDashboard'));
const CorporateParkingSpaces = React.lazy(() => import('./pages/Corporate/CorporateParkingSpaces'));
const CompanyMembers = React.lazy(() => import('./pages/Corporate/CompanyMembers'));
const CompanyAllocations = React.lazy(() => import('./pages/Corporate/CompanyAllocations'));
const CompanyBookings = React.lazy(() => import('./pages/Corporate/CompanyBookings'));
const CompanyInvoices = React.lazy(() => import('./pages/Corporate/CompanyInvoices'));
const CompanySettings = React.lazy(() => import('./pages/Corporate/CompanySettings'));
const AcceptInvitation = React.lazy(() => import('./pages/Corporate/AcceptInvitation'));
const OutboxAdmin = React.lazy(() => import('./pages/Admin/OutboxAdmin'));

function Loading() {
  return (
    <div className="loading" style={{ minHeight: '60vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
      <div className="spinner"></div>
    </div>
  );
}

function Header() {
  const { isAuthenticated, user, logout, isAdmin } = useAuth();
  const { isCorporateMode } = useCompany();
  const { unreadCount } = useChatContext();
  const navigate = useNavigate();
  const [profileOpen, setProfileOpen] = React.useState(false);
  const profileRef = React.useRef(null);
  const [pendingRequests, setPendingRequests] = React.useState(0);

  const handleLogout = async () => {
    setProfileOpen(false);
    await logout();
    navigate('/login');
  };

  // Close dropdown on outside click
  React.useEffect(() => {
    const handler = (e) => {
      if (profileRef.current && !profileRef.current.contains(e.target)) {
        setProfileOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const { subscribeToRefresh } = useNotificationContext();

  React.useEffect(() => {
    let mounted = true;
    const fetchPendingCount = async () => {
      if (isAuthenticated) {
        try {
          const response = await api.getPendingRequestsCount();
          if (response?.success && mounted) {
            setPendingRequests(response.data);
          }
        } catch (error) {
          console.error("Failed to fetch pending requests count:", error);
        }
      }
    };

    fetchPendingCount();

    let unsubscribe = () => { };
    if (isAuthenticated && subscribeToRefresh) {
      unsubscribe = subscribeToRefresh(
        'HeaderPendingCount',
        [
          'booking.requested',
          'booking.approved',
          'booking.rejected',
          'extension.requested',
          'extension.approved',
          'extension.rejected'
        ],
        () => {
          fetchPendingCount();
        }
      );
    }

    return () => {
      mounted = false;
      unsubscribe();
    };
  }, [isAuthenticated, subscribeToRefresh]);

  const initials = user
    ? `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()
    : '';

  return (
    <header className="header">
      <div className="container header-content">
        <Link to="/" className="logo">ParkEase</Link>
        <nav className="nav">
          <Link to="/search" className="nav-link">Find Parking</Link>

          {isAuthenticated ? (
            <>
              {/* Messages with badge */}
              <Link to="/chat" className="nav-link" style={{ position: 'relative', display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                Messages
                {unreadCount > 0 && (
                  <span style={{
                    background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
                    color: 'white',
                    borderRadius: '10px',
                    padding: '1px 7px',
                    fontSize: '0.7rem',
                    fontWeight: '700',
                    minWidth: '18px',
                    textAlign: 'center',
                    lineHeight: '1.4',
                  }}>
                    {unreadCount > 99 ? '99+' : unreadCount}
                  </span>
                )}
              </Link>

              {/* Notification Bell */}
              <NotificationDropdown />
              
              {/* Company Switcher */}
              <CompanySwitcher />

              {/* Profile Avatar Dropdown */}
              <div ref={profileRef} style={{ position: 'relative' }}>
                <button
                  onClick={() => setProfileOpen(prev => !prev)}
                  title={`${user?.firstName} ${user?.lastName}`}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    background: 'transparent',
                    border: '2px solid rgba(255,255,255,0.15)',
                    borderRadius: '999px',
                    padding: '4px 12px 4px 4px',
                    cursor: 'pointer',
                    color: 'inherit',
                    transition: 'border-color 0.2s, background 0.2s',
                  }}
                  onMouseEnter={e => e.currentTarget.style.borderColor = 'rgba(99,102,241,0.6)'}
                  onMouseLeave={e => {
                    if (!profileOpen) e.currentTarget.style.borderColor = 'rgba(255,255,255,0.15)';
                  }}
                >
                  {/* Avatar circle */}
                  <span style={{
                    width: '30px',
                    height: '30px',
                    borderRadius: '50%',
                    background: 'linear-gradient(135deg, #6366f1, #8b5cf6)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontWeight: '700',
                    fontSize: '0.75rem',
                    color: 'white',
                    flexShrink: 0,
                  }}>
                    {initials || '?'}
                  </span>
                  <span style={{ fontSize: '0.875rem', fontWeight: '500', maxWidth: '90px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {user?.firstName}
                  </span>
                  {/* Caret */}
                  <svg width="10" height="6" viewBox="0 0 10 6" fill="none" style={{ transition: 'transform 0.2s', transform: profileOpen ? 'rotate(180deg)' : 'rotate(0)' }}>
                    <path d="M1 1l4 4 4-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </button>

                {/* Dropdown panel */}
                {profileOpen && (
                  <div style={{
                    position: 'absolute',
                    top: 'calc(100% + 10px)',
                    right: 0,
                    background: '#1e293b',
                    border: '1px solid rgba(255,255,255,0.08)',
                    borderRadius: '14px',
                    boxShadow: '0 20px 50px rgba(0,0,0,0.5)',
                    minWidth: '200px',
                    overflow: 'hidden',
                    zIndex: 8000,
                    animation: 'profileDropIn 0.18s ease-out',
                  }}>
                    {/* User info header */}
                    <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid rgba(255,255,255,0.07)' }}>
                      <div style={{ fontWeight: '600', fontSize: '0.9rem', color: 'white' }}>
                        {user?.firstName} {user?.lastName}
                      </div>
                      <div style={{ fontSize: '0.76rem', color: '#64748b', marginTop: '2px' }}>
                        ParkEase User
                      </div>
                    </div>

                    {/* Links */}
                    {(isCorporateMode ? [
                      { to: '/corporate/dashboard', icon: '🏢', label: 'Corporate Dash' },
                      { to: '/corporate/parking-spaces', icon: '🏗️', label: 'Parking Inventory' },
                      { to: '/corporate/members', icon: '👥', label: 'Members' },
                      { to: '/corporate/allocations', icon: '🅿️', label: 'Allocations' },
                      { to: '/corporate/bookings', icon: '📅', label: 'Corp Bookings' },
                      { to: '/corporate/invoices', icon: '🧾', label: 'Invoices' },
                      { to: '/corporate/settings', icon: '⚙️', label: 'Company Settings' },
                      { to: '/profile', icon: '👤', label: 'My Profile' },
                      ...(isAdmin ? [{ to: '/admin/outbox', icon: '📬', label: 'Outbox Admin' }] : []),
                    ] : [
                      { to: '/dashboard', icon: '🏠', label: 'Dashboard' },
                      { to: '/bookings', icon: '📅', label: 'My Bookings' },
                      { to: '/garage', icon: '🚗', label: 'My Garage' },
                      { to: '/favorites', icon: '❤️', label: 'Favorites' },
                      { to: '/profile', icon: '👤', label: 'My Profile' },
                      { to: '/my/listings', icon: '💰', label: 'My Listings' },
                      { to: '/my/requests', icon: '📋', label: 'Vendor Inbox', badge: pendingRequests > 0 ? pendingRequests : null },
                      ...(isAdmin ? [{ to: '/admin/outbox', icon: '📬', label: 'Outbox Admin' }] : []),
                    ]).map(item => (
                      <Link
                        key={item.to}
                        to={item.to}
                        onClick={() => setProfileOpen(false)}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '10px',
                          padding: '0.65rem 1.25rem',
                          color: '#cbd5e1',
                          textDecoration: 'none',
                          fontSize: '0.875rem',
                          transition: 'background 0.15s, color 0.15s',
                        }}
                        onMouseEnter={e => { e.currentTarget.style.background = 'rgba(255,255,255,0.06)'; e.currentTarget.style.color = 'white'; }}
                        onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.color = '#cbd5e1'; }}
                      >
                        <span style={{ fontSize: '1rem', width: '20px', textAlign: 'center' }}>{item.icon}</span>
                        {item.label}
                        {item.badge != null && (
                          <span style={{
                            marginLeft: 'auto',
                            background: '#ef4444',
                            color: 'white',
                            borderRadius: '10px',
                            padding: '2px 6px',
                            fontSize: '0.7rem',
                            fontWeight: '700',
                          }}>
                            {item.badge > 99 ? '99+' : item.badge}
                          </span>
                        )}
                      </Link>
                    ))}

                    {/* Divider + Logout */}
                    <div style={{ borderTop: '1px solid rgba(255,255,255,0.07)', margin: '4px 0' }} />
                    <button
                      onClick={handleLogout}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '10px',
                        padding: '0.65rem 1.25rem',
                        width: '100%',
                        background: 'transparent',
                        border: 'none',
                        color: '#f87171',
                        fontSize: '0.875rem',
                        cursor: 'pointer',
                        textAlign: 'left',
                        transition: 'background 0.15s',
                      }}
                      onMouseEnter={e => e.currentTarget.style.background = 'rgba(239,68,68,0.08)'}
                      onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                    >
                      <span style={{ fontSize: '1rem', width: '20px', textAlign: 'center' }}>🚪</span>
                      Logout
                    </button>
                  </div>
                )}
              </div>

              <style>{`
                @keyframes profileDropIn {
                  from { opacity: 0; transform: translateY(-6px) scale(0.97); }
                  to   { opacity: 1; transform: translateY(0) scale(1); }
                }
              `}</style>
            </>
          ) : (
            <>
              <Link to="/login" className="btn btn-secondary">Login</Link>
              <Link to="/register" className="btn btn-primary">Sign Up</Link>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}

function ProtectedRoute({ children }) {
  const { isAuthenticated, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <Loading />;
  }

  if (!isAuthenticated) {
    const returnUrl = `${location.pathname}${location.search || ''}`;
    const to = returnUrl && returnUrl !== '/'
      ? `/login?returnUrl=${encodeURIComponent(returnUrl)}`
      : '/login';
    return <Navigate to={to} replace />;
  }

  return children;
}

function safeReturnPath(raw) {
  if (!raw || typeof raw !== 'string') return null;
  if (!raw.startsWith('/') || raw.startsWith('//')) return null;
  return raw;
}

function AppRoutes() {
  const { isAuthenticated } = useAuth();
  const [searchParams] = useSearchParams();
  const authedHome = safeReturnPath(searchParams.get('returnUrl')) || '/dashboard';

  return (
    <Suspense fallback={<Loading />}>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/search" element={<Search />} />
        <Route path="/parking/:id" element={<ParkingDetails />} />
        <Route
          path="/login"
          element={isAuthenticated ? <Navigate to={authedHome} replace /> : <Login />}
        />
        <Route
          path="/register"
          element={isAuthenticated ? <Navigate to={authedHome} replace /> : <Register />}
        />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <Dashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="/bookings"
          element={
            <ProtectedRoute>
              <MyBookings />
            </ProtectedRoute>
          }
        />
        <Route
          path="/favorites"
          element={
            <ProtectedRoute>
              <MyFavorites />
            </ProtectedRoute>
          }
        />
        <Route
          path="/garage"
          element={
            <ProtectedRoute>
              <MyGarage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/my/listings"
          element={
            <ProtectedRoute>
              <VendorListings />
            </ProtectedRoute>
          }
        />
        <Route
          path="/my/requests"
          element={
            <ProtectedRoute>
              <VendorBookings />
            </ProtectedRoute>
          }
        />
        <Route
          path="/chat/:conversationId?"
          element={
            <ProtectedRoute>
              <Chat />
            </ProtectedRoute>
          }
        />
        <Route
          path="/profile"
          element={
            <ProtectedRoute>
              <Profile />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/dashboard"
          element={
            <ProtectedRoute>
              <CorporateDashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/parking-spaces"
          element={
            <ProtectedRoute>
              <CorporateParkingSpaces />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/members"
          element={
            <ProtectedRoute>
              <CompanyMembers />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/allocations"
          element={
            <ProtectedRoute>
              <CompanyAllocations />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/bookings"
          element={
            <ProtectedRoute>
              <CompanyBookings />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/invoices"
          element={
            <ProtectedRoute>
              <CompanyInvoices />
            </ProtectedRoute>
          }
        />
        <Route
          path="/corporate/settings"
          element={
            <ProtectedRoute>
              <CompanySettings />
            </ProtectedRoute>
          }
        />
        <Route
          path="/invite/accept/:token"
          element={
            <ProtectedRoute>
              <AcceptInvitation />
            </ProtectedRoute>
          }
        />
        <Route
          path="/admin/outbox"
          element={
            <ProtectedRoute>
              <OutboxAdmin />
            </ProtectedRoute>
          }
        />
        <Route path="*" element={<Navigate to="/" />} />
      </Routes>
    </Suspense>
  );
}

function Footer() {
  return (
    <footer style={{
      borderTop: '1px solid var(--color-border)',
      padding: '2rem 0',
      textAlign: 'center',
      color: 'var(--color-text-muted)',
    }}>
      <div className="container">
        <p>&copy; {new Date().getFullYear()} ParkEase. All rights reserved.</p>
        <p style={{ marginTop: '0.5rem', fontSize: '0.9rem' }}>
          Find and book parking spaces instantly.
        </p>
      </div>
    </footer>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <CompanyProvider>
          <NotificationProvider>
            <ChatProvider>
              <Toaster
                position="top-right"
              reverseOrder={false}
              gutter={12}
              toastOptions={{
                duration: 6000,
                style: {
                  background: '#1e293b',
                  color: '#f1f5f9',
                  border: '1px solid #334155',
                  padding: '14px 16px',
                  borderRadius: '8px',
                  boxShadow: '0 10px 40px rgba(0,0,0,0.5)',
                  fontSize: '14px',
                  maxWidth: '420px',
                  cursor: 'pointer',
                },
                success: {
                  duration: 5000,
                  style: {
                    background: '#064e3b',
                    border: '1px solid #10b981',
                  },
                  iconTheme: {
                    primary: '#10b981',
                    secondary: 'white',
                  },
                },
                error: {
                  duration: 8000,
                  style: {
                    background: '#450a0a',
                    border: '1px solid #ef4444',
                  },
                  iconTheme: {
                    primary: '#ef4444',
                    secondary: 'white',
                  },
                },
              }}
            />
            <Header />
            <AppRoutes />
            <Footer />
          </ChatProvider>
          </NotificationProvider>
        </CompanyProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
