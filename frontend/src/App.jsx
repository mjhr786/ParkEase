import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate, useLocation, useSearchParams } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ChatProvider, useChatContext } from './contexts/ChatContext';
import { NotificationProvider, useNotificationContext } from './context/NotificationContext';
import { CompanyProvider, useCompany } from './contexts/CompanyContext';
import { useTheme } from './contexts/ThemeContext';
import NotificationDropdown from './components/NotificationDropdown';
import CompanySwitcher from './components/CompanySwitcher';
import ThemeToggle from './components/ThemeToggle';
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
const LprSimulator = React.lazy(() => import('./pages/Admin/LprSimulator'));
const EvChargeSimulator = React.lazy(() => import('./pages/Admin/EvChargeSimulator'));
const AdminLayout = React.lazy(() => import('./pages/Admin/AdminLayout'));
const AdminDashboard = React.lazy(() => import('./pages/Admin/AdminDashboard'));
const AdminUsers = React.lazy(() => import('./pages/Admin/AdminUsers'));
const AdminUserDetail = React.lazy(() => import('./pages/Admin/AdminUserDetail'));
const AdminListings = React.lazy(() => import('./pages/Admin/AdminListings'));
const AdminListingDetail = React.lazy(() => import('./pages/Admin/AdminListingDetail'));
const AdminBookings = React.lazy(() => import('./pages/Admin/AdminBookings'));
const AdminBookingDetail = React.lazy(() => import('./pages/Admin/AdminBookingDetail'));
const AdminPayments = React.lazy(() => import('./pages/Admin/AdminPayments'));
const AdminPaymentDetail = React.lazy(() => import('./pages/Admin/AdminPaymentDetail'));
const AdminAuditLog = React.lazy(() => import('./pages/Admin/AdminAuditLog'));
const AdminRoute = React.lazy(() => import('./components/AdminRoute'));
const LprRegistry = React.lazy(() => import('./pages/Vendor/LprRegistry'));
const AccessPassScanner = React.lazy(() => import('./pages/Vendor/AccessPassScanner'));
const EventPackagesVendor = React.lazy(() => import('./pages/Vendor/EventPackages'));
const EventPackagesBrowse = React.lazy(() => import('./pages/EventPackagesBrowse'));

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
  const location = useLocation();
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
      if (!isAuthenticated) {
        if (mounted) setPendingRequests(0);
        return;
      }
      try {
        const response = await api.getPendingRequestsCount();
        if (response?.success && mounted) {
          const count = typeof response.data === 'number'
            ? response.data
            : Number(response.data) || 0;
          setPendingRequests(count);
        }
      } catch (error) {
        console.error("Failed to fetch pending requests count:", error);
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
          'booking.cancelled',
          'extension.requested',
          'extension.approved',
          'extension.rejected'
        ],
        () => {
          // Small delay so backend cache invalidation from the mutation is visible
          setTimeout(fetchPendingCount, 150);
        }
      );
    }

    // Re-sync badge when returning to the tab (covers missed SignalR / local actions)
    const onVisibility = () => {
      if (document.visibilityState === 'visible') {
        fetchPendingCount();
      }
    };
    document.addEventListener('visibilitychange', onVisibility);

    return () => {
      mounted = false;
      unsubscribe();
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, [isAuthenticated, subscribeToRefresh]);

  // Re-fetch badge when opening Vendor Inbox so a stale count clears even if events were missed
  React.useEffect(() => {
    if (!isAuthenticated || location.pathname !== '/my/requests') return;
    let cancelled = false;
    (async () => {
      try {
        const response = await api.getPendingRequestsCount();
        if (!cancelled && response?.success) {
          const count = typeof response.data === 'number'
            ? response.data
            : Number(response.data) || 0;
          setPendingRequests(count);
        }
      } catch {
        /* ignore */
      }
    })();
    return () => { cancelled = true; };
  }, [isAuthenticated, location.pathname]);

  const initials = user
    ? `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()
    : '';

  return (
    <header className="header">
      <div className="container header-content">
        <Link to="/" className="logo">ParkEase</Link>
        <nav className="nav">
          <Link to="/search" className="nav-link">Find Parking</Link>
          <Link to="/events" className="nav-link">Events</Link>

          <ThemeToggle />

          {isAuthenticated ? (
            <>
              {/* Messages with badge (same red style as conversation unread chips) */}
              <Link to="/chat" className="nav-link" style={{ position: 'relative', display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                Messages
                {Number(unreadCount) > 0 && (
                  <span
                    aria-label={`${unreadCount} unread messages`}
                    style={{
                      background: 'var(--color-danger)',
                      color: 'var(--color-text-on-accent)',
                      borderRadius: '999px',
                      padding: '0 6px',
                      fontSize: '0.7rem',
                      fontWeight: '700',
                      minWidth: '18px',
                      height: '18px',
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      lineHeight: 1,
                      boxShadow: '0 0 0 2px var(--badge-ring)',
                    }}
                  >
                    {Number(unreadCount) > 99 ? '99+' : Number(unreadCount)}
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
                    border: '2px solid var(--control-border)',
                    borderRadius: '999px',
                    padding: '4px 12px 4px 4px',
                    cursor: 'pointer',
                    color: 'inherit',
                    transition: 'border-color 0.2s, background 0.2s',
                  }}
                  onMouseEnter={e => { e.currentTarget.style.borderColor = 'var(--color-accent)'; }}
                  onMouseLeave={e => {
                    if (!profileOpen) e.currentTarget.style.borderColor = 'var(--control-border)';
                  }}
                >
                  {/* Avatar circle */}
                  <span style={{
                    width: '30px',
                    height: '30px',
                    borderRadius: '50%',
                    background: 'var(--gradient-primary)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontWeight: '700',
                    fontSize: '0.75rem',
                    color: 'var(--color-text-on-accent)',
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
                    background: 'var(--dropdown-bg)',
                    border: '1px solid var(--dropdown-border)',
                    borderRadius: '14px',
                    boxShadow: 'var(--shadow-dropdown)',
                    minWidth: '200px',
                    overflow: 'hidden',
                    zIndex: 8000,
                    animation: 'profileDropIn 0.18s ease-out',
                  }}>
                    {/* User info header */}
                    <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid var(--dropdown-border)' }}>
                      <div style={{ fontWeight: '600', fontSize: '0.9rem', color: 'var(--color-text-primary)' }}>
                        {user?.firstName} {user?.lastName}
                      </div>
                      <div style={{ fontSize: '0.76rem', color: 'var(--dropdown-muted)', marginTop: '2px' }}>
                        {isAdmin ? 'Platform Admin' : 'ParkEase User'}
                      </div>
                    </div>

                    {/* Links — platform admins get admin entry points only (not consumer/vendor menus) */}
                    {(isAdmin ? [
                      { to: '/admin', icon: '🛡️', label: 'Admin Panel' },
                      { to: '/admin/users', icon: '👥', label: 'Manage Users' },
                      { to: '/admin/audit', icon: '📝', label: 'Audit Log' },
                      { to: '/admin/outbox', icon: '📬', label: 'Outbox' },
                      { to: '/tools/lpr-simulator', icon: '📷', label: 'LPR Simulator' },
                      { to: '/tools/ev-charge-simulator', icon: '⚡', label: 'EV Charge Simulator' },
                    ] : isCorporateMode ? [
                      { to: '/corporate/dashboard', icon: '🏢', label: 'Corporate Dash' },
                      { to: '/corporate/parking-spaces', icon: '🏗️', label: 'Parking Inventory' },
                      { to: '/corporate/members', icon: '👥', label: 'Members' },
                      { to: '/corporate/allocations', icon: '🅿️', label: 'Allocations' },
                      { to: '/corporate/bookings', icon: '📅', label: 'Corp Bookings' },
                      { to: '/corporate/invoices', icon: '🧾', label: 'Invoices' },
                      { to: '/corporate/settings', icon: '⚙️', label: 'Company Settings' },
                      { to: '/profile', icon: '👤', label: 'My Profile' },
                      { to: '/tools/lpr-simulator', icon: '📷', label: 'LPR Simulator' },
                      { to: '/tools/ev-charge-simulator', icon: '⚡', label: 'EV Charge Simulator' },
                    ] : [
                      { to: '/dashboard', icon: '🏠', label: 'Dashboard' },
                      { to: '/bookings', icon: '📅', label: 'My Bookings' },
                      { to: '/garage', icon: '🚗', label: 'My Garage' },
                      { to: '/favorites', icon: '❤️', label: 'Favorites' },
                      { to: '/profile', icon: '👤', label: 'My Profile' },
                      { to: '/my/listings', icon: '💰', label: 'My Listings' },
                      { to: '/my/event-packages', icon: '🎟️', label: 'Event packages' },
                      { to: '/my/requests', icon: '📋', label: 'Vendor Inbox', badge: pendingRequests > 0 ? pendingRequests : null },
                      { to: '/my/access-scan', icon: '📱', label: 'Scan access pass' },
                      { to: '/tools/lpr-simulator', icon: '📷', label: 'LPR Simulator' },
                      { to: '/tools/ev-charge-simulator', icon: '⚡', label: 'EV Charge Simulator' },
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
                          color: 'var(--dropdown-item)',
                          textDecoration: 'none',
                          fontSize: '0.875rem',
                          transition: 'background 0.15s, color 0.15s',
                        }}
                        onMouseEnter={e => {
                          e.currentTarget.style.background = 'var(--dropdown-item-hover-bg)';
                          e.currentTarget.style.color = 'var(--color-text-primary)';
                        }}
                        onMouseLeave={e => {
                          e.currentTarget.style.background = 'transparent';
                          e.currentTarget.style.color = 'var(--dropdown-item)';
                        }}
                      >
                        <span style={{ fontSize: '1rem', width: '20px', textAlign: 'center' }}>{item.icon}</span>
                        {item.label}
                        {item.badge != null && (
                          <span style={{
                            marginLeft: 'auto',
                            background: 'var(--color-danger)',
                            color: 'var(--color-text-on-accent)',
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
                    <div style={{ borderTop: '1px solid var(--dropdown-border)', margin: '4px 0' }} />
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
                        color: 'var(--color-error)',
                        fontSize: '0.875rem',
                        cursor: 'pointer',
                        textAlign: 'left',
                        transition: 'background 0.15s',
                      }}
                      onMouseEnter={e => { e.currentTarget.style.background = 'var(--color-primary-alpha)'; }}
                      onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; }}
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
  const { isAuthenticated, isAdmin } = useAuth();
  const [searchParams] = useSearchParams();
  const returnPath = safeReturnPath(searchParams.get('returnUrl'));
  const authedHome = returnPath || (isAdmin ? '/admin' : '/dashboard');

  return (
    <Suspense fallback={<Loading />}>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/search" element={<Search />} />
        <Route path="/events" element={<EventPackagesBrowse />} />
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
          path="/my/access-scan"
          element={
            <ProtectedRoute>
              <AccessPassScanner />
            </ProtectedRoute>
          }
        />
        <Route
          path="/my/event-packages"
          element={
            <ProtectedRoute>
              <EventPackagesVendor />
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
        {/* Platform admin console — separate shell, Admin-only */}
        <Route
          path="/admin"
          element={
            <AdminRoute>
              <AdminLayout />
            </AdminRoute>
          }
        >
          <Route index element={<AdminDashboard />} />
          <Route path="users" element={<AdminUsers />} />
          <Route path="users/:id" element={<AdminUserDetail />} />
          <Route path="listings" element={<AdminListings />} />
          <Route path="listings/:id" element={<AdminListingDetail />} />
          <Route path="bookings" element={<AdminBookings />} />
          <Route path="bookings/:id" element={<AdminBookingDetail />} />
          <Route path="payments" element={<AdminPayments />} />
          <Route path="payments/:id" element={<AdminPaymentDetail />} />
          <Route path="audit" element={<AdminAuditLog />} />
          <Route path="outbox" element={<OutboxAdmin />} />
        </Route>

        {/* Vendor / authenticated tools — NOT AdminRoute (must not break owners) */}
        <Route
          path="/tools/lpr-simulator"
          element={
            <ProtectedRoute>
              <LprSimulator />
            </ProtectedRoute>
          }
        />
        <Route
          path="/tools/ev-charge-simulator"
          element={
            <ProtectedRoute>
              <EvChargeSimulator />
            </ProtectedRoute>
          }
        />
        {/* Legacy paths preserved */}
        <Route path="/admin/lpr-simulator" element={<Navigate to="/tools/lpr-simulator" replace />} />
        <Route path="/admin/ev-charge-simulator" element={<Navigate to="/tools/ev-charge-simulator" replace />} />
        <Route
          path="/my/listings/:parkingSpaceId/lpr"
          element={
            <ProtectedRoute>
              <LprRegistry />
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

function AppShell() {
  const location = useLocation();
  const isAdminConsole = location.pathname === '/admin' || location.pathname.startsWith('/admin/');
  // Platform admin console uses its own layout (no consumer header/footer).
  // Legacy /admin/lpr|ev simulators redirect to /tools/* so they never hit AdminLayout.

  return (
    <>
      {!isAdminConsole && <Header />}
      <main className={isAdminConsole ? undefined : 'main-content'} style={isAdminConsole ? { padding: 0, margin: 0 } : undefined}>
        <AppRoutes />
      </main>
      {!isAdminConsole && <Footer />}
    </>
  );
}

function ThemedToaster() {
  // Re-render when theme changes so toast styles pick up new CSS variables
  useTheme();

  return (
    <Toaster
      position="top-right"
      reverseOrder={false}
      gutter={12}
      toastOptions={{
        duration: 6000,
        style: {
          background: 'var(--toast-bg)',
          color: 'var(--toast-color)',
          border: '1px solid var(--toast-border)',
          padding: '14px 16px',
          borderRadius: '8px',
          boxShadow: 'var(--shadow-lg)',
          fontSize: '14px',
          maxWidth: '420px',
          cursor: 'pointer',
        },
        success: {
          duration: 5000,
          style: {
            background: 'var(--toast-success-bg)',
            border: '1px solid var(--toast-success-border)',
            color: 'var(--toast-color)',
          },
          iconTheme: {
            primary: 'var(--color-success)',
            secondary: 'var(--color-text-on-accent)',
          },
        },
        error: {
          duration: 8000,
          style: {
            background: 'var(--toast-error-bg)',
            border: '1px solid var(--toast-error-border)',
            color: 'var(--toast-color)',
          },
          iconTheme: {
            primary: 'var(--color-error)',
            secondary: 'var(--color-text-on-accent)',
          },
        },
      }}
    />
  );
}

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <CompanyProvider>
          <NotificationProvider>
            <ChatProvider>
              <ThemedToaster />
              <AppShell />
            </ChatProvider>
          </NotificationProvider>
        </CompanyProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
