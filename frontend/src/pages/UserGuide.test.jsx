import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import UserGuide from './UserGuide';

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    isAuthenticated: true,
    user: {
      email: 'driver@parkease.com',
      firstName: 'Rahul',
      lastName: 'Sharma',
    },
    isCorporateChannel: false,
    isAdmin: false,
  }),
}));

function renderUserGuide() {
  return render(
    <MemoryRouter>
      <UserGuide />
    </MemoryRouter>
  );
}

describe('UserGuide Component', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    cleanup();
  });

  it('renders the master title, hero badges, and quick stats', () => {
    renderUserGuide();
    expect(screen.getByText(/ParkEase Knowledge Hub & Master User Guide/i)).toBeInTheDocument();
    expect(screen.getByText(/Official Documentation · V4.0.0/i)).toBeInTheDocument();
    expect(screen.getByText(/Booking Lifecycle Machine/i)).toBeInTheDocument();
    expect(screen.getByText(/4W & 2W Capacity Guards/i)).toBeInTheDocument();
  });

  it('renders all persona tabs', () => {
    renderUserGuide();
    expect(screen.getByText('Marketplace Driver')).toBeInTheDocument();
    expect(screen.getByText('Parking Host / Vendor')).toBeInTheDocument();
    expect(screen.getByText('Corporate Enterprise')).toBeInTheDocument();
    expect(screen.getByText('Platform Administrator')).toBeInTheDocument();
    expect(screen.getByText('Hardware & IoT Simulators')).toBeInTheDocument();
    expect(screen.getByText('System Architecture')).toBeInTheDocument();
  });

  it('allows checking and unchecking items in the 5-minute onboarding roadmap', async () => {
    const user = userEvent.setup();
    renderUserGuide();

    const checklistItem = screen.getByText(/Register Your Vehicle in "My Garage"/i);
    expect(checklistItem).toBeInTheDocument();

    // Click to toggle
    await user.click(checklistItem);

    // Progress counter should increment
    expect(screen.getByText(/1 of 5 Completed/i)).toBeInTheDocument();

    // Click again to uncheck
    await user.click(checklistItem);
    expect(screen.getByText(/0 of 5 Completed/i)).toBeInTheDocument();
  });

  it('switches persona workflows when clicking persona tabs', async () => {
    const user = userEvent.setup();
    renderUserGuide();

    // Default is Marketplace Driver
    expect(screen.getByText(/Finding & Filtering Parking Spaces/i)).toBeInTheDocument();

    // Switch to Host / Vendor
    const hostTab = screen.getByRole('button', { name: /Parking Host \/ Vendor/i });
    await user.click(hostTab);

    expect(screen.getByText(/The 7-Step Listing Creation Wizard/i)).toBeInTheDocument();
    expect(screen.getByText(/Managing Booking Inquiries & Approvals/i)).toBeInTheDocument();

    // Switch to Corporate Enterprise
    const corpTab = screen.getByRole('button', { name: /Corporate Enterprise/i });
    await user.click(corpTab);

    expect(screen.getByText(/Dual-Pool Slot Allocation & Waitlist Promotions/i)).toBeInTheDocument();
    expect(screen.getByText(/Enterprise SSO \(SAML 2.0 \/ OIDC\) & SCIM Sync/i)).toBeInTheDocument();
  });

  it('filters workflows when typing in the search bar', async () => {
    const user = userEvent.setup();
    renderUserGuide();

    const searchInput = screen.getByPlaceholderText(/Search workflows, FAQs, features/i);
    await user.type(searchInput, 'Garage');

    expect(screen.getByText(/Managing Your Vehicles in "My Garage"/i)).toBeInTheDocument();
    expect(screen.queryByText(/The 7-Step Listing Creation Wizard/i)).not.toBeInTheDocument();
  });

  it('allows clicking states in the 10-state booking machine to inspect details', async () => {
    const user = userEvent.setup();
    renderUserGuide();

    const confirmedStateBtn = screen.getByRole('button', { name: /Confirmed/i });
    await user.click(confirmedStateBtn);

    expect(screen.getByText(/State 1: Confirmed/i)).toBeInTheDocument();
    expect(screen.getByText(/Payment captured and spot reserved/i)).toBeInTheDocument();
  });

  it('updates pricing in the interactive fare calculator', async () => {
    const user = userEvent.setup();
    renderUserGuide();

    // Default rate is ₹60/hr for 4W for 3 hrs => subtotal 180 + fee 18 + tax 36 = 234
    expect(screen.getByText(/Total Payable Amount:/i)).toBeInTheDocument();

    // Switch to 2-Wheeler (40% discount)
    const bikeBtn = screen.getByRole('button', { name: /Bike\/Scooter/i });
    await user.click(bikeBtn);

    expect(screen.getByText(/2W Discounted/i)).toBeInTheDocument();
  });

  it('toggles FAQ accordion questions', async () => {
    const user = userEvent.setup();
    renderUserGuide();

    const faqQuestion = screen.getByText(/What is the IST-safe UTC Noon Date-Anchoring engine\?/i);
    expect(faqQuestion).toBeInTheDocument();

    await user.click(faqQuestion);
    expect(screen.getByText(/In day-based and multi-day bookings, Indian Standard Time/i)).toBeInTheDocument();
  });
});
