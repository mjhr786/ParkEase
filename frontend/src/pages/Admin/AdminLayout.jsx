import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

const navItems = [
  { to: '/admin', end: true, label: 'Dashboard', icon: '📊' },
  { to: '/admin/users', label: 'Users', icon: '👥' },
  { to: '/admin/listings', label: 'Listings', icon: '🅿️' },
  { to: '/admin/bookings', label: 'Bookings', icon: '📅' },
  { to: '/admin/payments', label: 'Payments', icon: '💳' },
  { to: '/admin/audit', label: 'Audit log', icon: '📝' },
  { to: '/admin/outbox', label: 'Outbox', icon: '📬' },
];

const linkStyle = ({ isActive }) => ({
  display: 'flex',
  alignItems: 'center',
  gap: '10px',
  padding: '0.7rem 1rem',
  borderRadius: '10px',
  textDecoration: 'none',
  fontSize: '0.9rem',
  fontWeight: isActive ? 600 : 500,
  color: isActive ? 'var(--color-text-on-accent)' : 'var(--color-text-secondary)',
  background: isActive ? 'var(--color-primary-alpha)' : 'transparent',
  border: isActive ? '1px solid var(--color-primary)' : '1px solid transparent',
  marginBottom: '4px',
});

/**
 * Separate platform-admin shell — no consumer marketplace chrome.
 */
export default function AdminLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const initials = user
    ? `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()
    : 'A';

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      background: 'var(--color-bg-primary)',
      color: 'var(--color-text-primary)',
    }}>
      <aside style={{
        width: '240px',
        flexShrink: 0,
        borderRight: '1px solid var(--color-border)',
        background: 'var(--color-bg-secondary)',
        padding: '1.25rem 0.9rem',
        display: 'flex',
        flexDirection: 'column',
      }}>
        <div style={{ padding: '0.25rem 0.75rem 1.25rem', borderBottom: '1px solid var(--color-border)', marginBottom: '1rem' }}>
          <div style={{ fontWeight: 800, fontSize: '1.05rem', letterSpacing: '-0.02em' }}>
            ParkEase <span style={{ color: 'var(--color-accent-light)' }}>Admin</span>
          </div>
          <div style={{ fontSize: '0.72rem', color: 'var(--color-text-muted)', marginTop: '4px' }}>
            Platform control plane
          </div>
        </div>

        <nav style={{ flex: 1 }}>
          {navItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} style={linkStyle}>
              <span style={{ width: 22, textAlign: 'center' }}>{item.icon}</span>
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div style={{
          borderTop: '1px solid var(--color-border)',
          paddingTop: '1rem',
          marginTop: '0.5rem',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '0 0.5rem 0.75rem' }}>
            <span style={{
              width: 34,
              height: 34,
              borderRadius: '50%',
              background: 'var(--gradient-primary)',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '0.75rem',
              fontWeight: 700,
            }}>
              {initials}
            </span>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontSize: '0.85rem', fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {user?.firstName} {user?.lastName}
              </div>
              <div style={{ fontSize: '0.7rem', color: 'var(--color-text-muted)' }}>Administrator</div>
            </div>
          </div>
          <button
            type="button"
            onClick={handleLogout}
            style={{
              width: '100%',
              background: 'transparent',
              border: '1px solid rgba(248,113,113,0.25)',
              color: 'var(--color-error)',
              borderRadius: '10px',
              padding: '0.55rem 0.75rem',
              cursor: 'pointer',
              fontSize: '0.85rem',
            }}
          >
            Sign out
          </button>
        </div>
      </aside>

      <main style={{ flex: 1, minWidth: 0, padding: '1.5rem 1.75rem 2.5rem', overflow: 'auto' }}>
        <Outlet />
      </main>
    </div>
  );
}
