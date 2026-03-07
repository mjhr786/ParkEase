import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ChatProvider, useChatContext } from './contexts/ChatContext';
import { NotificationProvider, useNotificationContext } from './context/NotificationContext';
import NotificationDropdown from './components/NotificationDropdown';
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

function Loading() {
  return (
    <div className="loading" style={{ minHeight: '60vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
      <div className="spinner"></div>
    </div>
  );
}

function Header() {
  const { isAuthenticated, user, logout, isVendor } = useAuth();
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
      if (isAuthenticated && isVendor) {
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
    if (isAuthenticated && isVendor && subscribeToRefresh) {
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
  }, [isAuthenticated, isVendor, subscribeToRefresh]);

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
                        {isVendor ? '⭐ Vendor' : 'Member'}
                      </div>
                    </div>

                    {/* Links */}
                    {[
                      { to: '/dashboard', icon: '🏠', label: 'Dashboard' },
                      { to: '/bookings', icon: '📅', label: 'My Bookings' },
                      { to: '/garage', icon: '🚗', label: 'My Garage' },
                      { to: '/favorites', icon: '❤️', label: 'Favorites' },
                      { to: '/profile', icon: '👤', label: 'My Profile' },
                      ...(isVendor ? [
                        { to: '/vendor/listings', icon: '🅿️', label: 'My Listings' },
                        { to: '/vendor/bookings', icon: '📋', label: 'Requests', badge: pendingRequests > 0 ? pendingRequests : null },
                      ] : []),
                    ].map(item => (
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

function ProtectedRoute({ children, vendorOnly = false }) {
  const { isAuthenticated, loading, isVendor } = useAuth();

  if (loading) {
    return <Loading />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" />;
  }

  if (vendorOnly && !isVendor) {
    return <Navigate to="/dashboard" />;
  }

  return children;
}

function AppRoutes() {
  const { isAuthenticated } = useAuth();

  return (
    <Suspense fallback={<Loading />}>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/search" element={<Search />} />
        <Route path="/parking/:id" element={<ParkingDetails />} />
        <Route
          path="/login"
          element={isAuthenticated ? <Navigate to="/dashboard" /> : <Login />}
        />
        <Route
          path="/register"
          element={isAuthenticated ? <Navigate to="/dashboard" /> : <Register />}
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
          path="/vendor/listings"
          element={
            <ProtectedRoute vendorOnly>
              <VendorListings />
            </ProtectedRoute>
          }
        />
        <Route
          path="/vendor/bookings"
          element={
            <ProtectedRoute vendorOnly>
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
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
