import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { NotificationProvider } from './context/NotificationContext';
import toast, { Toaster } from 'react-hot-toast';
import Home from './pages/Home';
import Login from './pages/Login';
import Register from './pages/Register';
import Search from './pages/Search';
import ParkingDetails from './pages/ParkingDetails';
import Dashboard from './pages/Dashboard';
import MyBookings from './pages/MyBookings';
import VendorListings from './pages/VendorListings';
import VendorBookings from './pages/VendorBookings';
import './index.css';

function Header() {
  const { isAuthenticated, user, logout, isVendor } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <header className="header">
      <div className="container header-content">
        <Link to="/" className="logo">ParkEase</Link>
        <nav className="nav">
          <Link to="/search" className="nav-link">Find Parking</Link>
          {isAuthenticated ? (
            <>
              <Link to="/dashboard" className="nav-link">Dashboard</Link>
              <Link to="/bookings" className="nav-link">My Bookings</Link>
              {isVendor && (
                <>
                  <Link to="/vendor/listings" className="nav-link">My Listings</Link>
                  <Link to="/vendor/bookings" className="nav-link">Requests</Link>
                </>
              )}
              <span className="nav-link" style={{ color: 'var(--color-text-muted)' }}>
                Hi, {user?.firstName}
              </span>
              <button onClick={handleLogout} className="btn btn-secondary" style={{ padding: '0.5rem 1rem' }}>
                Logout
              </button>
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
    return (
      <div className="loading" style={{ minHeight: '100vh' }}>
        <div className="spinner"></div>
      </div>
    );
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
      <Route path="*" element={<Navigate to="/" />} />
    </Routes>
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
        </NotificationProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
