import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate, useLocation, useSearchParams } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ChatProvider, useChatContext } from './contexts/ChatContext';
import { NotificationProvider, useNotificationContext } from './context/NotificationContext';
import { CompanyProvider } from './contexts/CompanyContext';
import { useTheme } from './contexts/ThemeContext';
import NotificationDropdown from './components/NotificationDropdown';
import CompanySwitcher from './components/CompanySwitcher';
import CorporateChannelRoute from './components/CorporateChannelRoute';
import ThemeToggle from './components/ThemeToggle';
import toast, { Toaster } from 'react-hot-toast';
import React, { Suspense } from 'react';
import './index.css';
import api from './services/api';
import { postAuthDestination } from './utils/safeReturnUrl';

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
const CorporateLogin = React.lazy(() => import('./pages/Corporate/CorporateLogin'));
const CreateCompany = React.lazy(() => import('./pages/Corporate/CreateCompany'));
const LeaseBrowse = React.lazy(() => import('./pages/Corporate/LeaseBrowse'));
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
  const { isAuthenticated, user, logout, isAdmin, channel, isCorporateChannel } = useAuth();
  const { unreadCount } = useChatContext();
  const navigate = useNavigate();
  const location = useLocation();
  const [profileOpen, setProfileOpen] = React.useState(false);
  const profileRef = React.useRef(null);
  const [pendingRequests, setPendingRequests] = React.useState(0);
  /** Group keys expanded in the profile menu (e.g. "account", "hosting") */
  const [openMenuGroups, setOpenMenuGroups] = React.useState(() => new Set());

  // PR10b: JWT channel alone drives chrome (soft isCorporateMode toggle removed)
  const showCorporateChrome = isCorporateChannel;
  const homePath = showCorporateChrome ? '/corporate/dashboard' : '/';

  const profileMenuGroups = React.useMemo(() => {
    if (isAdmin) {
      return [
        {
          key: 'admin',
          label: 'Administration',
          icon: '🛡️',
          items: [
            { to: '/admin', icon: '🛡️', label: 'Admin Panel' },
            { to: '/admin/users', icon: '👥', label: 'Manage Users' },
            { to: '/admin/audit', icon: '📝', label: 'Audit Log' },
            { to: '/admin/outbox', icon: '📬', label: 'Outbox' },
          ],
        },
        {
          key: 'tools',
          label: 'Tools',
          icon: '🧰',
          items: [
            { to: '/tools/lpr-simulator', icon: '📷', label: 'LPR Simulator' },
            { to: '/tools/ev-charge-simulator', icon: '⚡', label: 'EV Charge Simulator' },
          ],
        },
      ];
    }
    if (showCorporateChrome) {
      return [
        {
          key: 'company',
          label: 'Company',
          icon: '🏢',
          items: [
            { to: '/corporate/dashboard', icon: '🏢', label: 'Corporate Dash' },
            { to: '/corporate/parking-spaces', icon: '🏗️', label: 'Parking Inventory' },
            { to: '/corporate/members', icon: '👥', label: 'Members' },
            { to: '/corporate/allocations', icon: '🅿️', label: 'Allocations' },
            { to: '/corporate/lease-browse', icon: '🔍', label: 'Lease Browse' },
            { to: '/corporate/bookings', icon: '📅', label: 'Corp Bookings' },
            { to: '/corporate/invoices', icon: '🧾', label: 'Invoices' },
            { to: '/corporate/settings', icon: '⚙️', label: 'Company Settings' },
          ],
        },
        {
          key: 'account',
          label: 'Account',
          icon: '👤',
          items: [
            { to: '/profile', icon: '👤', label: 'My Profile' },
          ],
        },
        {
          key: 'tools',
          label: 'Tools',
          icon: '🧰',
          items: [
            { to: '/tools/lpr-simulator', icon: '📷', label: 'LPR Simulator' },
            { to: '/tools/ev-charge-simulator', icon: '⚡', label: 'EV Charge Simulator' },
          ],
        },
      ];
    }
    return [
      {
        key: 'account',
        label: 'My Account',
        icon: '👤',
        items: [
          { to: '/dashboard', icon: '🏠', label: 'Dashboard' },
          { to: '/bookings', icon: '📅', label: 'My Bookings' },
          { to: '/garage', icon: '🚗', label: 'My Garage' },
          { to: '/favorites', icon: '❤️', label: 'Favorites' },
          { to: '/profile', icon: '👤', label: 'My Profile' },
        ],
      },
      {
        key: 'hosting',
        label: 'Hosting',
        icon: '💰',
        badge: pendingRequests > 0 ? pendingRequests : null,
        items: [
          { to: '/my/listings', icon: '💰', label: 'My Listings' },
          { to: '/my/event-packages', icon: '🎟️', label: 'Event packages' },
          { to: '/my/requests', icon: '📋', label: 'Vendor Inbox', badge: pendingRequests > 0 ? pendingRequests : null },
          { to: '/my/access-scan', icon: '📱', label: 'Scan access pass' },
        ],
      },
      {
        key: 'tools',
        label: 'Tools',
        icon: '🧰',
        items: [
          { to: '/tools/lpr-simulator', icon: '📷', label: 'LPR Simulator' },
          { to: '/tools/ev-charge-simulator', icon: '⚡', label: 'EV Charge Simulator' },
        ],
      },
    ];
  }, [isAdmin, showCorporateChrome, pendingRequests]);

  const handleLogout = async () => {
    setProfileOpen(false);
    setOpenMenuGroups(new Set());
    await logout();
    navigate(channel === 'Corporate' ? '/corporate/login' : '/login');
  };

  const toggleMenuGroup = (key) => {
    setOpenMenuGroups((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
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

  // When opening the profile menu, expand groups that match the current route
  React.useEffect(() => {
    if (!profileOpen) return;
    const path = location.pathname;
    const activeKeys = profileMenuGroups
      .filter((g) => g.items.some((item) => path === item.to || path.startsWith(`${item.to}/`)))
      .map((g) => g.key);
    // Always open at least the first group if nothing matches
    setOpenMenuGroups(new Set(activeKeys.length ? activeKeys : [profileMenuGroups[0]?.key].filter(Boolean)));
  }, [profileOpen, location.pathname, profileMenuGroups]);

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
    <header className="header" data-shell={showCorporateChrome ? 'corporate' : 'marketplace'}>
      <div className="container header-content">
        <Link to={isAuthenticated ? homePath : '/'} className="logo">
          ParkEase
          {showCorporateChrome && (
            <span style={{ fontWeight: 500, fontSize: '0.75rem', marginLeft: '8px', opacity: 0.85 }}>
              Corporate
            </span>
          )}
        </Link>
        <nav className="nav">
          {showCorporateChrome ? (
            <>
              <Link to="/corporate/dashboard" className="nav-link">Dashboard</Link>
              <Link to="/corporate/parking-spaces" className="nav-link">Inventory</Link>
              <Link to="/corporate/bookings" className="nav-link">Bookings</Link>
            </>
          ) : (
            <>
              <Link to="/search" className="nav-link">Find Parking</Link>
              <Link to="/events" className="nav-link">Events</Link>
            </>
          )}

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
              
              {/* Corporate company switcher (Corporate channel only — no marketplace link) */}
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
                    minWidth: '230px',
                    maxHeight: 'min(70vh, 520px)',
                    overflowY: 'auto',
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

                    {/* Grouped menus with collapsible submenus */}
                    {profileMenuGroups.map((group) => {
                      const isOpen = openMenuGroups.has(group.key);
                      const groupBadge = group.badge != null ? group.badge : null;
                      return (
                        <div key={group.key}>
                          <button
                            type="button"
                            onClick={() => toggleMenuGroup(group.key)}
                            aria-expanded={isOpen}
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: '10px',
                              padding: '0.65rem 1.25rem',
                              width: '100%',
                              background: 'transparent',
                              border: 'none',
                              color: 'var(--color-text-primary)',
                              fontSize: '0.8rem',
                              fontWeight: 600,
                              letterSpacing: '0.02em',
                              cursor: 'pointer',
                              textAlign: 'left',
                              transition: 'background 0.15s',
                            }}
                            onMouseEnter={e => {
                              e.currentTarget.style.background = 'var(--dropdown-item-hover-bg)';
                            }}
                            onMouseLeave={e => {
                              e.currentTarget.style.background = 'transparent';
                            }}
                          >
                            <span style={{ fontSize: '1rem', width: '20px', textAlign: 'center' }}>{group.icon}</span>
                            <span style={{ flex: 1 }}>{group.label}</span>
                            {groupBadge != null && (
                              <span style={{
                                background: 'var(--color-danger)',
                                color: 'var(--color-text-on-accent)',
                                borderRadius: '10px',
                                padding: '2px 6px',
                                fontSize: '0.7rem',
                                fontWeight: '700',
                              }}>
                                {groupBadge > 99 ? '99+' : groupBadge}
                              </span>
                            )}
                            <svg
                              width="10"
                              height="6"
                              viewBox="0 0 10 6"
                              fill="none"
                              aria-hidden="true"
                              style={{
                                transition: 'transform 0.2s',
                                transform: isOpen ? 'rotate(180deg)' : 'rotate(0)',
                                opacity: 0.7,
                                flexShrink: 0,
                              }}
                            >
                              <path d="M1 1l4 4 4-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            </svg>
                          </button>

                          {isOpen && group.items.map((item) => (
                            <Link
                              key={item.to}
                              to={item.to}
                              onClick={() => setProfileOpen(false)}
                              style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '10px',
                                padding: '0.55rem 1.25rem 0.55rem 2.5rem',
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
                        </div>
                      );
                    })}

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

function AppRoutes() {
  const { isAuthenticated, isAdmin, isCorporateChannel, isBootstrap } = useAuth();
  const [searchParams] = useSearchParams();
  // Already-authenticated users hitting /login or /register land on the product dashboard
  // for their JWT channel (marketplace → /dashboard, corporate → /corporate/dashboard).
  const authedHome = postAuthDestination(isCorporateChannel ? 'corporate' : 'marketplace', {
    returnUrl: searchParams.get('returnUrl'),
    isAdmin,
    isBootstrap,
  });

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
        <Route path="/corporate/login" element={<CorporateLogin />} />
        <Route
          path="/corporate/create-company"
          element={
            <CorporateChannelRoute allowBootstrap>
              <CreateCompany />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/dashboard"
          element={
            <CorporateChannelRoute>
              <CorporateDashboard />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/parking-spaces"
          element={
            <CorporateChannelRoute>
              <CorporateParkingSpaces />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/members"
          element={
            <CorporateChannelRoute>
              <CompanyMembers />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/allocations"
          element={
            <CorporateChannelRoute>
              <CompanyAllocations />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/lease-browse"
          element={
            <CorporateChannelRoute>
              <LeaseBrowse />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/bookings"
          element={
            <CorporateChannelRoute>
              <CompanyBookings />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/invoices"
          element={
            <CorporateChannelRoute>
              <CompanyInvoices />
            </CorporateChannelRoute>
          }
        />
        <Route
          path="/corporate/settings"
          element={
            <CorporateChannelRoute>
              <CompanySettings />
            </CorporateChannelRoute>
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
