import React, { useState, useMemo, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import './UserGuide.css';

// =============================================================================
// Static Data: Personas, Workflows, State Machine, Notifications, FAQs, Glossary
// =============================================================================

const ONBOARDING_CHECKLIST_ITEMS = [
  {
    id: 'step_register',
    title: 'Create Your Account & Secure Your Profile',
    desc: 'Sign up with email or 1-click Google/Apple OAuth and enforce a strong password with live policy checks.',
    linkText: 'Sign Up / Login',
    linkTo: '/login',
  },
  {
    id: 'step_garage',
    title: 'Register Your Vehicle in "My Garage"',
    desc: 'Add your 4-Wheeler (car) or 2-Wheeler (bike) license plate to unlock automated LPR access.',
    linkText: 'Open My Garage',
    linkTo: '/garage',
  },
  {
    id: 'step_search',
    title: 'Discover Parking via Near-Me GPS or City Search',
    desc: 'Filter by distance radius, vehicle class (4W vs 2W), amenities (EV, CCTV), and check ML availability forecast.',
    linkText: 'Find Parking',
    linkTo: '/search',
  },
  {
    id: 'step_book',
    title: 'Confirm Booking via Stripe 3DS & Get Digital Pass',
    desc: 'Calculate itemized hourly or daily fare with IST-safe UTC noon anchors and receive instant QR access pass.',
    linkText: 'Search & Book',
    linkTo: '/search',
  },
  {
    id: 'step_host',
    title: 'Explore Hosting or Corporate Channel',
    desc: 'List your private driveway/lot in 7 simple steps or switch to your corporate workplace portal.',
    linkText: 'Host Your Space',
    linkTo: '/my/listings',
  },
];

const PERSONAS = [
  {
    id: 'driver',
    name: 'Marketplace Driver',
    icon: '🚗',
    roleTag: 'Consumer / Commuter',
    headline: 'Seamlessly Find, Reserve & Park Across Cities',
    summary: 'Everything a driver needs to discover nearby parking spots, manage vehicles in My Garage, book with instant Stripe 3DS confirmation, check in via QR/LPR, and manage real-time extensions.',
  },
  {
    id: 'vendor',
    name: 'Parking Host / Vendor',
    icon: '🅿️',
    roleTag: 'Property Owner / Space Host',
    headline: 'Monetize Parking Inventory with Complete Control',
    summary: 'A complete playbook for space hosts: 7-step listing wizard, independent 4W & 2W capacity pools, instant vs manual booking approvals, QR pass scanner, schedule blackout dates, and LPR camera API keys.',
  },
  {
    id: 'corporate',
    name: 'Corporate Enterprise',
    icon: '🏢',
    roleTag: 'Workplace Manager & Staff',
    headline: 'Enterprise Dual-Pool Allocations & SSO',
    summary: 'Enterprise workspace parking management: dual-pool 2W/4W allocation guards, automated waitlist promotions, long-term commercial lot leases, consolidated invoicing, and SAML/OIDC SSO.',
  },
  {
    id: 'admin',
    name: 'Platform Administrator',
    icon: '🛡️',
    roleTag: 'Super Admin & Governance',
    headline: 'System Moderation, Outbox & Global SSO Locks',
    summary: 'Super Admin operations: platform health telemetry, user and listing moderation, Stripe PaymentIntent refunds, Transactional Outbox inspection, and centralized Corporate SSO oversight.',
  },
  {
    id: 'iot',
    name: 'Hardware & IoT Simulators',
    icon: '⚡',
    roleTag: 'Gate Hardware & Field Devices',
    headline: 'Automated License Plate Recognition & EV Stations',
    summary: 'Field hardware testing environment: LPR camera entry/exit simulator with space-scoped keys, plate deny blocklist enforcement, and EV charging station kWh surcharge simulators.',
  },
  {
    id: 'architecture',
    name: 'System Architecture',
    icon: '📐',
    roleTag: 'Principal Architect Blueprint',
    headline: 'Enterprise Invariants, UTC Anchors & Outbox Pattern',
    summary: 'Comprehensive technical blueprint: React 18 SPA + ASP.NET Web API + PostgreSQL PostGIS + Upstash Redis, Channel Isolation security model, IST-safe UTC noon anchors, and eventual consistency.',
  },
];

const NOTIFICATION_CATEGORIES = [
  {
    type: 'BookingRequest',
    icon: '📥',
    color: 'var(--color-primary)',
    label: 'Booking Request Received',
    desc: 'Triggered when a driver requests manual approval for a spot. Host receives real-time SignalR banner in Vendor Inbox.',
  },
  {
    type: 'BookingConfirmed',
    icon: '✅',
    color: 'var(--color-success)',
    label: 'Booking Confirmed & QR Ready',
    desc: 'Dispatched immediately upon successful Stripe payment capture or host approval. Digital QR access pass becomes active.',
  },
  {
    type: 'BookingRejected',
    icon: '❌',
    color: 'var(--color-error)',
    label: 'Booking Rejected with Reason',
    desc: 'Sent when a host declines a request with a mandatory explanation. Any payment authorization hold is instantly released.',
  },
  {
    type: 'PaymentReceived',
    icon: '💰',
    color: 'var(--color-success)',
    label: 'Payment Captured',
    desc: 'Stripe webhook confirms payment intent succeeded. An itemized invoice receipt is generated and emailed to the driver.',
  },
  {
    type: 'NewMessage',
    icon: '💬',
    color: 'var(--color-secondary)',
    label: 'In-App Direct Chat Message',
    desc: 'Real-time WebSocket chat message from driver or space host. Updates the unread badge counter in the top header.',
  },
  {
    type: 'SystemAlert',
    icon: '🔔',
    color: 'var(--color-warning)',
    label: 'System & Security Alert',
    desc: 'Surfaced for platform-wide announcements, password changes, or administrative account actions.',
  },
];

const OVERSTAY_ALERTS = [
  {
    type: 'booking.session.ending',
    badge: '30-Min Reminder',
    color: 'var(--color-warning)',
    text: 'Sent 30 minutes before your scheduled booking expires, prompting you to extend or prepare for exit.',
  },
  {
    type: 'booking.overstay',
    badge: 'Grace Period Expired',
    color: 'var(--color-danger)',
    text: 'Triggered after the 15-minute post-expiration grace period has elapsed without vehicle checkout.',
  },
  {
    type: 'booking.overstay.fee',
    badge: 'Surcharge Calculated',
    color: 'var(--color-danger)',
    text: 'Overstay background worker calculates incremental hourly penalty and attaches it to the final ledger.',
  },
  {
    type: 'booking.overstay.autocheckout',
    badge: 'Auto-Checkout Finalized',
    color: 'var(--color-text-muted)',
    text: 'Terminal system check-out triggered when facility closing hours are reached, releasing the space for cleaning.',
  },
];

const WORKFLOWS = [
  // --- DRIVER WORKFLOWS ---
  {
    id: 'wf_driver_search',
    persona: 'driver',
    stepNumber: 'WF-01',
    title: 'Finding & Filtering Parking Spaces',
    objective: 'Locate available, verified parking spots near your destination with real-time ML availability forecasts.',
    actionLabel: 'Try Live Search',
    actionUrl: '/search',
    steps: [
      {
        heading: 'Choose Search Mode (City vs Near-Me GPS)',
        text: 'From the Home page or Search screen, either enter a destination city/area or click "Near Me" to open the radius slider (1 km to 50 km). Grant browser GPS permission when prompted.',
      },
      {
        heading: 'Select Vehicle Class Filter (4-Wheeler vs 2-Wheeler)',
        text: 'ParkEase enforces separate physical capacity pools. Filter specifically for "4-Wheeler" (cars, SUVs) or "2-Wheeler" (motorcycles, scooters) to see only spots matching your vehicle type.',
      },
      {
        heading: 'Apply Amenity & Price Filters',
        text: 'Refine results by EV Charging station, CCTV surveillance, 24/7 access, security guard, covered roof, or gate code access. Use the price slider to set your hourly budget in ₹.',
      },
      {
        heading: 'Inspect ML.NET Availability Forecast & Booked Slots Grid',
        text: 'On the Spot Detail page, review the 24-hour occupancy chart powered by ML.NET forecasting. Check the interactive timeline grid showing existing booked blocks to spot open time windows.',
      },
    ],
    callout: {
      type: 'info',
      icon: '💡',
      title: 'Pro-Tip: Near-Me Geolocation',
      body: 'If GPS permission was accidentally blocked in your browser, click the lock icon in the browser address bar, reset Location permission to "Allow", and click "Near Me" again.',
    },
  },
  {
    id: 'wf_driver_garage',
    persona: 'driver',
    stepNumber: 'WF-02',
    title: 'Managing Your Vehicles in "My Garage"',
    objective: 'Maintain an accurate digital fleet so bookings and automated gate cameras automatically recognize your vehicle.',
    actionLabel: 'Go to My Garage',
    actionUrl: '/garage',
    steps: [
      {
        heading: 'Navigate to My Garage',
        text: 'Open the user avatar dropdown and click "My Garage" (/garage). Here you can inspect all registered vehicles.',
      },
      {
        heading: 'Add Vehicle Details',
        text: 'Click "+ Add Vehicle". Enter your license plate (letters are automatically converted to uppercase), Make & Model (e.g., Honda City, Ather 450X), Color, and select Class (4-Wheeler or 2-Wheeler).',
      },
      {
        heading: 'Set Primary Vehicle',
        text: 'Mark one vehicle as "Default". When you create a parking reservation, your primary vehicle is pre-selected for maximum speed.',
      },
      {
        heading: 'Vehicle Deletion Invariant',
        text: 'The system protects active parking sessions: any vehicle currently checked into a space or having an upcoming confirmed reservation cannot be deleted until the booking completes.',
      },
    ],
    callout: {
      type: 'architecture',
      icon: '🔒',
      title: 'Architectural Invariant: Class Verification',
      body: 'Spots strictly validate vehicle class against physical inventory pools. Booking a 2-Wheeler slot using a 4-Wheeler vehicle is blocked on the server with a 422 Unprocessable Entity error.',
    },
  },
  {
    id: 'wf_driver_booking',
    persona: 'driver',
    stepNumber: 'WF-03',
    title: 'Booking Calculation, Coupons & Stripe Checkout',
    objective: 'Understand fare computation, apply coupons, and complete secure 3D-Secure payment.',
    actionLabel: 'Browse Available Spots',
    actionUrl: '/search',
    steps: [
      {
        heading: 'Choose Rate Model (Hourly vs Daily Max vs Monthly Pass)',
        text: 'Select your reservation window. The booking calculator automatically compares hourly cumulative rate against the daily maximum rate cap and applies the most cost-effective tier.',
      },
      {
        heading: 'Itemized Fare Breakdown',
        text: 'The calculator transparently breaks down: Base Parking Fee + 10% Platform Service Fee + 18% GST (Tax) - Promo Discount = Total Payable in ₹.',
      },
      {
        heading: 'Apply Promo / Coupon Code',
        text: 'Enter a valid coupon code and click Validate. POST /api/coupons/validate verifies expiration and discount rules before checkout.',
      },
      {
        heading: 'Stripe 3D-Secure Payment',
        text: 'Complete payment with card credentials via encrypted Stripe Elements. Complete 3DS OTP verification if challenged. Upon capture, status transitions to "Confirmed" and a receipt is issued.',
      },
    ],
    callout: {
      type: 'info',
      icon: '🕒',
      title: 'IST-Safe UTC Noon Date Anchoring Engine',
      body: 'Day-based reservations anchor dates at 12:00:00 UTC (Noon). This prevents Asia/Kolkata (UTC+5:30) midnight shifts from inadvertently pushing timestamps into previous calendar days and charging an extra day!',
    },
  },
  {
    id: 'wf_driver_active',
    persona: 'driver',
    stepNumber: 'WF-04',
    title: 'Active Booking, QR Pass, Check-In & Extensions',
    objective: 'Manage active parking sessions, monitor countdowns, and request extensions if delayed.',
    actionLabel: 'View Active Bookings',
    actionUrl: '/bookings',
    steps: [
      {
        heading: 'Locate Booking & Access QR Pass',
        text: 'Go to "My Bookings" (/bookings) or your Driver Dashboard. Click your booking card to expand your digital QR Access Pass and parking bay details.',
      },
      {
        heading: 'Gate Entry & Automated Check-In',
        text: 'Present the QR code at the facility scanner or simply drive up to an LPR-equipped gate. Once scanned, booking status automatically transitions to "In Progress".',
      },
      {
        heading: 'Monitor Live Countdown Timer',
        text: 'The dashboard displays a live countdown timer. When fewer than 30 minutes remain, the timer changes to an amber "Ending Soon" alert banner.',
      },
      {
        heading: 'Request a Session Extension',
        text: 'If delayed, click "Extend Booking". Choose +1 Hour, +2 Hours, or Custom Duration. The system verifies slot availability, calculates the incremental fare, and handles instant payment or host approval.',
      },
      {
        heading: 'Check-Out & Review',
        text: 'Upon exit scan or manual check-out, duration is finalized to "Completed". You can submit a 1-to-5 star rating with feedback and photos.',
      },
    ],
    callout: {
      type: 'warning',
      icon: '⚠️',
      title: 'Overstay Policy & Grace Period',
      body: 'ParkEase provides a 15-minute grace period after scheduled expiration. Beyond this, overstay notifications fire and host-configured overstay surcharges are assessed.',
    },
  },
  {
    id: 'wf_driver_favorites_share',
    persona: 'driver',
    stepNumber: 'WF-05',
    title: 'Favorites, Bookmarks & Multi-Channel Sharing',
    objective: 'Save your favorite spots for one-click rebooking and share verified parking links with colleagues.',
    actionLabel: 'View My Favorites',
    actionUrl: '/favorites',
    steps: [
      {
        heading: 'Save Spots to Favorites',
        text: 'Click the heart icon (❤️) on any spot card or detail page. Bookmarked spots are stored under /favorites for instant access without re-searching.',
      },
      {
        heading: 'Interactive Photo Gallery & Booked Slots Grid',
        text: 'Spot detail pages feature an interactive Cloudflare R2 photo lightbox and a visual timeline grid showing booked blocks to identify open reservation gaps.',
      },
      {
        heading: 'Share via WhatsApp, Email & Web Share API',
        text: 'Click the Share button on any parking detail page. Choose WhatsApp to open pre-formatted message deep-links, Email for pre-filled templates, or Native Share on mobile devices.',
      },
    ],
    callout: {
      type: 'info',
      icon: '📲',
      title: 'Direct Deep Linking',
      body: 'Shared links include canonical parking identifiers (/parking/:id) allowing recipients to inspect coordinates and rates instantly.',
    },
  },
  {
    id: 'wf_driver_chat_notif',
    persona: 'driver',
    stepNumber: 'WF-06',
    title: 'In-App Direct Chat & Real-Time SignalR Notifications',
    objective: 'Communicate directly with spot hosts and stay notified of gate activity and session alerts.',
    actionLabel: 'Open Direct Chat',
    actionUrl: '/chat',
    steps: [
      {
        heading: 'Start a Chat Thread with Spot Host',
        text: 'From any confirmed booking or spot page, click "Message Host". This opens /chat with the specific parking space context attached.',
      },
      {
        heading: 'Real-Time WebSocket Messaging',
        text: 'Chat messages are exchanged in real time via SignalR (/hubs/chat). Read receipts and unread badge chips keep you informed of replies.',
      },
      {
        heading: 'Notification Bell & Overstay Banners',
        text: 'The header bell icon aggregates 6 notification categories. Click "Mark All as Read" to clear badges, or click any notification to jump directly to the relevant booking.',
      },
    ],
    callout: {
      type: 'info',
      icon: '💬',
      title: 'Privacy Preserved',
      body: 'In-app chat eliminates the need to share personal mobile phone numbers between drivers and hosts, ensuring full privacy.',
    },
  },
  {
    id: 'wf_driver_social_auth',
    persona: 'driver',
    stepNumber: 'WF-07',
    title: 'Social Login (Google & Apple) & Linked Profile Security',
    objective: 'Leverage frictionless 1-click authentication and manage linked credentials securely.',
    actionLabel: 'Edit Profile Security',
    actionUrl: '/profile',
    steps: [
      {
        heading: '1-Click Google Sign-In (GIS)',
        text: 'Click "Continue with Google" on /login. Google Identity Services dynamically injects the Google SDK and exchanges verified ID tokens with ParkEase API.',
      },
      {
        heading: 'Apple Sign-In with Cryptographic Nonce',
        text: 'Apple Sign-In utilizes an ephemeral SHA-256 cryptographic nonce to prevent replay attacks and ensure multi-platform security.',
      },
      {
        heading: 'Set Password for Social Accounts (/profile)',
        text: 'Users registered via Google or Apple can set a standalone password in their Profile. The live password policy checklist verifies uppercase, lowercase, and digits.',
      },
      {
        heading: 'Safe Return URL Protection',
        text: 'Deep-linked destinations (?returnUrl=...) are strictly validated against an internal allowlist, preventing open-redirect phishing attacks.',
      },
    ],
    callout: {
      type: 'architecture',
      icon: '🔐',
      title: 'Social Identity Linkage',
      body: 'Linking a password to your social account enables you to log in with either Google/Apple or traditional email/password credentials interchangeably.',
    },
  },

  // --- HOST / VENDOR WORKFLOWS ---
  {
    id: 'wf_vendor_listing',
    persona: 'vendor',
    stepNumber: 'WF-08',
    title: 'The 7-Step Listing Creation Wizard',
    objective: 'Publish your parking facility or private driveway with precise PostGIS GPS coordinates and tiered rates.',
    actionLabel: 'Manage Listings',
    actionUrl: '/my/listings',
    steps: [
      {
        heading: 'Step 1: General Details',
        text: 'Provide a catchy title, comprehensive space description, entry instructions (e.g. gate code, intercom), and host contact phone.',
      },
      {
        heading: 'Step 2: Location & PostGIS Geocoding',
        text: 'Enter street address, city, state, and pin code. Drag the map pin on the interactive Leaflet map to pinpoint the exact entrance coordinates stored as PostGIS POINT geometry.',
      },
      {
        heading: 'Step 3: Dual Capacity Pools (4W & 2W)',
        text: 'Specify how many 4-Wheeler cars and how many 2-Wheeler motorcycles can park simultaneously. Each pool operates with independent availability counters.',
      },
      {
        heading: 'Step 4: Tiered Rates (Hourly, Daily, Monthly)',
        text: 'Set your Hourly Rate (₹/hr), 24-Hour Daily Maximum Cap (₹/day), and optional Monthly Pass Rate (₹/month).',
      },
      {
        heading: 'Step 5: Amenity Flags',
        text: 'Toggle amenities: EV Charging Station, CCTV Cameras, 24/7 Access, Security Guard On-Duty, Covered Roof, Gate Code Entry.',
      },
      {
        heading: 'Step 6: High-Resolution Photo Uploads',
        text: 'Upload crisp photos directly to Cloudflare R2 object storage via secure pre-signed URLs. Set your featured cover photo.',
      },
      {
        heading: 'Step 7: Publishing Mode (Instant vs Manual Approval)',
        text: 'Choose "Instant Booking" for automatic confirmation upon payment, or "Manual Approval" if you prefer reviewing each driver request.',
      },
    ],
    callout: {
      type: 'info',
      icon: '📍',
      title: 'Geospatial Precision',
      body: 'Accurate pin positioning ensures drivers navigating via Google Maps or Apple Maps arrive at the exact driveway gate rather than a generic street center.',
    },
  },
  {
    id: 'wf_vendor_inbox',
    persona: 'vendor',
    stepNumber: 'WF-09',
    title: 'Managing Booking Inquiries & Approvals',
    objective: 'Review driver requests, approve or decline with feedback, and track incoming revenue.',
    actionLabel: 'Open Vendor Inbox',
    actionUrl: '/my/requests',
    steps: [
      {
        heading: 'Access Vendor Inbox',
        text: 'Open "Vendor Inbox" (/my/requests). The top header displays a live red badge for pending requests synced in real time via SignalR.',
      },
      {
        heading: 'Inspect Request Details',
        text: 'Review driver name, vehicle license plate, vehicle class, start and end time window, and expected net payout in ₹.',
      },
      {
        heading: 'One-Click Approval',
        text: 'Click "Approve" to lock the slot. The driver receives an instant SignalR push notification and their card is captured.',
      },
      {
        heading: 'Rejection with Mandatory Reason',
        text: 'If space is unavailable, click "Reject". Select or input a mandatory rejection reason (e.g. driveway maintenance). The driver is notified immediately and authorization holds are voided.',
      },
    ],
    callout: {
      type: 'info',
      icon: '⚡',
      title: 'Instant SignalR Synchronization',
      body: 'Vendor Inbox listens to WebSocket events. When a driver submits a booking request, the inbox refreshes without requiring a manual page reload.',
    },
  },
  {
    id: 'wf_vendor_overrides',
    persona: 'vendor',
    stepNumber: 'WF-10',
    title: 'Schedule Overrides, Blackout Dates & Listing Suspension',
    objective: 'Block specific dates for maintenance or holidays and manage active listing visibility.',
    actionLabel: 'Listing Settings',
    actionUrl: '/my/listings',
    steps: [
      {
        heading: 'Configure Calendar Blackout Windows',
        text: 'In your listing editor, select "Schedule Overrides". Add date ranges (e.g. holiday vacation or driveway resurfacing) where the spot cannot be reserved.',
      },
      {
        heading: 'Temporary Listing Suspension (Soft-Delete)',
        text: 'Need to pause hosting? Toggle listing status to "Suspended". Your listing is hidden from public search without deleting your ratings, reviews, or earnings history.',
      },
      {
        heading: 'Re-activate with 1 Click',
        text: 'Toggle the switch back to "Active" when ready to welcome guests again. Caches are invalidated instantly via Upstash Redis.',
      },
    ],
    callout: {
      type: 'info',
      icon: '📅',
      title: 'Zero Overbooking Guarantee',
      body: 'Schedule overrides take precedence over general hours, preventing drivers from booking during maintenance windows.',
    },
  },
  {
    id: 'wf_vendor_scanner_lpr',
    persona: 'vendor',
    stepNumber: 'WF-11',
    title: 'QR Pass Scanner & LPR Camera Registry',
    objective: 'Verify driver passes on-site and configure physical LPR camera keys and deny rules.',
    actionLabel: 'Open Pass Scanner',
    actionUrl: '/my/access-scan',
    steps: [
      {
        heading: 'Camera-Based QR Scanner',
        text: 'Open "Scan Access Pass" (/my/access-scan) on your phone or tablet browser. Allow camera access or manually type the 8-character pass code.',
      },
      {
        heading: 'Real-Time Validation',
        text: 'The scanner validates the pass against POST /api/passes/validate. A green "Access Granted" banner confirms driver name, plate, and time window; a red banner alerts for expired or invalid passes.',
      },
      {
        heading: 'Generate LPR Camera API Keys',
        text: 'In your listing settings, generate API Secret Keys for physical LPR cameras mounted at your gate. Each key is strictly scoped to your specific space ID.',
      },
      {
        heading: 'Plate Deny Blocklist Rules',
        text: 'Add problematic or unauthorized vehicle license plates to your facility blocklist. Denied plates are immediately rejected by gate cameras regardless of booking status.',
      },
    ],
    callout: {
      type: 'architecture',
      icon: '🛡️',
      title: 'Space-Scoped Key Security',
      body: 'Security rule LP5 strictly enforces that an LPR key provisioned for Space A cannot scan or grant access to Space B, preventing hardware cross-talk.',
    },
  },
  {
    id: 'wf_vendor_event_pkgs',
    persona: 'vendor',
    stepNumber: 'WF-12',
    title: 'Host Earnings Analytics & Flat-Rate Event Packages',
    objective: 'Analyze revenue metrics and create flat-rate parking packages for stadium concerts and festivals.',
    actionLabel: 'Event Packages',
    actionUrl: '/my/event-packages',
    steps: [
      {
        heading: 'Vendor Financial Dashboard',
        text: 'Inspect Gross Revenue, Net Payouts, and Platform Fees breakdown. Recharts trend lines display daily and weekly earnings.',
      },
      {
        heading: 'Create Flat-Rate Event Package',
        text: 'Open /my/event-packages. Specify event name (e.g. IPL Cricket Match), venue, reserved slot count, and fixed flat fee in ₹.',
      },
      {
        heading: 'Public Event Listing Directory',
        text: 'Published packages appear on the public /events browse page, enabling drivers attending the event to reserve in 1 click.',
      },
    ],
    callout: {
      type: 'info',
      icon: '🎟️',
      title: 'High-Demand Event Monetization',
      body: 'Flat-rate event packages protect hosts from under-pricing during stadium surges and give drivers guaranteed parking.',
    },
  },

  // --- CORPORATE ENTERPRISE WORKFLOWS ---
  {
    id: 'wf_corp_onboarding',
    persona: 'corporate',
    stepNumber: 'WF-13',
    title: 'Corporate Setup & Channel Isolation',
    objective: 'Register a corporate tenant, invite team members, and understand isolated JWT channel boundaries.',
    actionLabel: 'Corporate Dashboard',
    actionUrl: '/corporate/dashboard',
    steps: [
      {
        heading: 'Company Registration',
        text: 'Corporate admins register at /corporate/create-company with official organization details, registration number, and billing preferences.',
      },
      {
        heading: 'Channel Isolation Architecture',
        text: 'ParkEase enforces strict isolation between Consumer Marketplace and Corporate Enterprise domains. Corporate JWT tokens carry verified tenant claims and cannot be mingled with marketplace data.',
      },
      {
        heading: 'Company Switcher in Top Navigation',
        text: 'Users managing multiple corporate subsidiaries can switch active workspaces in 1 click using the "Company Switcher" dropdown in the header.',
      },
      {
        heading: 'Invite Employees & Assign Roles',
        text: 'Invite staff via email at /corporate/members with roles: Corporate Admin, Member, or Visitor. Staff receive secure tokenized invitation links.',
      },
    ],
    callout: {
      type: 'architecture',
      icon: '🏢',
      title: 'Channel Route Guards',
      body: 'Any attempt to access /corporate/* routes without a valid corporate-channel JWT immediately redirects to /dashboard, preventing unauthorized consumer access.',
    },
  },
  {
    id: 'wf_corp_dual_pool',
    persona: 'corporate',
    stepNumber: 'WF-14',
    title: 'Dual-Pool Slot Allocation & Waitlist Promotions',
    objective: 'Manage employee 4W and 2W parking quotas, fixed bays, and automated waitlist promotion.',
    actionLabel: 'Corporate Allocations',
    actionUrl: '/corporate/allocations',
    steps: [
      {
        heading: 'Configure 4W & 2W Physical Pools',
        text: 'In /corporate/allocations, use the dual-pool sliders to set dedicated limits for 4-Wheeler cars and 2-Wheeler two-wheelers. The total cannot exceed facility capacity.',
      },
      {
        heading: 'Assign Fixed Bays',
        text: 'Assign permanent bays (e.g. Slot B-04) to specific employee emails. Fixed bay holders enjoy guaranteed daily parking.',
      },
      {
        heading: 'Set Shared Pool Monthly Quotas',
        text: 'Configure monthly booking caps per employee (e.g. 15 bookings/month) to prevent hoarding of shared slots.',
      },
      {
        heading: 'Automated FIFO Waitlist Promotion',
        text: 'When all shared slots are booked, employees can join the waitlist. As soon as another employee cancels, the background worker automatically promotes the next person and dispatches a notification.',
      },
    ],
    callout: {
      type: 'info',
      icon: '🔄',
      title: 'Zero Admin Overhead Waitlist',
      body: 'Waitlist promotion happens asynchronously in the background. The promoted employee is automatically confirmed without requiring HR intervention.',
    },
  },
  {
    id: 'wf_corp_sso',
    persona: 'corporate',
    stepNumber: 'WF-15',
    title: 'Enterprise SSO (SAML 2.0 / OIDC) & SCIM Sync',
    objective: 'Configure Microsoft Entra ID (Azure AD), Okta, or Google Workspace Single Sign-On and enforce password-free login.',
    actionLabel: 'Corporate SSO Settings',
    actionUrl: '/corporate/settings',
    steps: [
      {
        heading: 'Select Protocol (OIDC or SAML 2.0)',
        text: 'In /corporate/settings, choose OIDC (recommended for Azure AD & Okta) or SAML 2.0. Input Client ID, Client Secret, and Discovery URL.',
      },
      {
        heading: 'Domain Whitelisting Routing',
        text: 'Specify your company email domains (e.g., acme.com). Any employee entering an @acme.com email at login is automatically redirected to your identity provider.',
      },
      {
        heading: 'Mandatory SSO Enforcement',
        text: 'Toggle "Disable Password Login". When active, employees cannot use traditional username/password credentials, ensuring total compliance with corporate security policies.',
      },
      {
        heading: 'SCIM 2.0 Employee Lifecycle Provisioning',
        text: 'Generate a SCIM 2.0 Bearer Token. Connect Azure AD or Okta SCIM sync to auto-provision new joiners and auto-revoke leavers in real time.',
      },
    ],
    callout: {
      type: 'warning',
      icon: '🛡️',
      title: 'Dry-Run Test Connection',
      body: 'Always click "Test SSO Connection" before enabling "Disable Password Login" to verify successful token exchange and prevent lockout.',
    },
  },
  {
    id: 'wf_corp_lease_invoicing',
    persona: 'corporate',
    stepNumber: 'WF-16',
    title: 'Commercial Lot Leases & Corporate Invoicing Lifecycle',
    objective: 'Contract long-term bulk parking and manage corporate billing with CSV financial exports.',
    actionLabel: 'Lease Marketplace',
    actionUrl: '/corporate/lease-browse',
    steps: [
      {
        heading: 'Commercial Lease Marketplace (/corporate/lease-browse)',
        text: 'Browse commercial parking facilities for 6, 12, or 24-month bulk leases. Configure requested 4W and 2W bay allocations with physical capacity guards.',
      },
      {
        heading: 'Corporate Invoicing Lifecycle (/corporate/invoices)',
        text: 'Track billing statements through Draft → Issued → Paid → Void states. Line items detail member usage, fixed bays, and overstay surcharges.',
      },
      {
        heading: 'Record Offline Bank Wire Payments',
        text: 'Admins can record corporate wire transfers and cheque numbers directly on the invoice record, marking it Paid with audit timestamps.',
      },
      {
        heading: 'Export Audit-Ready CSV Reports',
        text: 'Click "Export CSV" on the corporate dashboard to download corporate-dashboard-{YYYY-MM-DD}.csv for ERP expense reconciliation.',
      },
    ],
    callout: {
      type: 'info',
      icon: '🧾',
      title: 'ERP Compatible Billing',
      body: 'CSV exports include tax breakdowns, member email tags, and cost-center allocations for effortless import into SAP or Oracle NetSuite.',
    },
  },

  // --- ADMIN WORKFLOWS ---
  {
    id: 'wf_admin_console',
    persona: 'admin',
    stepNumber: 'WF-17',
    title: 'Super Admin Console & Moderation Engine',
    objective: 'Inspect platform health telemetry, moderate listings and users, and process Stripe refunds.',
    actionLabel: 'Launch Admin Console',
    actionUrl: '/admin',
    steps: [
      {
        heading: 'Platform Health & Telemetry Dashboard',
        text: 'At /admin, monitor real-time health for ASP.NET Web API, PostgreSQL database, Upstash Redis cache, and Transactional Outbox worker.',
      },
      {
        heading: 'User Moderation & Role Assignment',
        text: 'Inspect user accounts, assign roles (Driver, Vendor, CorporateAdmin, SuperAdmin), or suspend accounts with mandatory audit reasons (invalidates active sessions immediately).',
      },
      {
        heading: 'Listing Moderation & Geocoding Audits',
        text: 'Audit uploaded photos and coordinates. Flag or suspend listings that violate platform standards.',
      },
      {
        heading: 'Stripe Payment Lookup & Manual Refunds',
        text: 'Search charges by Stripe PaymentIntent ID. Issue full or partial refunds directly with automatic booking ledger adjustments.',
      },
    ],
    callout: {
      type: 'info',
      icon: '📝',
      title: 'Immutable Security Audit Log',
      body: 'Every administrative mutation is recorded in the platform audit log (/admin/audit) with timestamp, actor ID, category, and raw JSON diff.',
    },
  },
  {
    id: 'wf_admin_outbox_sso',
    persona: 'admin',
    stepNumber: 'WF-18',
    title: 'Transactional Outbox Queue & Global SSO Oversight',
    objective: 'Re-drive failed domain events and exercise emergency override locks over corporate SSO configurations.',
    actionLabel: 'Outbox Inspector',
    actionUrl: '/admin/outbox',
    steps: [
      {
        heading: 'Transactional Outbox Inspection (/admin/outbox)',
        text: 'Inspect domain events: BookingRequested, BookingApprovedEvent, BookingConfirmedEvent, PaymentCompletedEvent, and LprScanEvent. Filter by status (Pending, Processed, Failed).',
      },
      {
        heading: 'Manual Event Re-Drive',
        text: 'If downstream webhook delivery or email dispatch failed due to a transient network issue, click "Re-drive" to re-queue the event.',
      },
      {
        heading: 'Global Corporate SSO Oversight (/admin/corporate-sso)',
        text: 'Inspect all enterprise tenant SSO configurations in a single view: Live, Enabled not effective, Force-disabled, or Disabled.',
      },
      {
        heading: 'Emergency Platform Force-Disable Lock',
        text: 'In the event of a tenant security incident or credential leak, Super Admins can apply a "Force-Disable" lock that cannot be cleared by the tenant admin until resolved.',
      },
    ],
    callout: {
      type: 'architecture',
      icon: '⚙️',
      title: 'Zero Data Loss Outbox Pattern',
      body: 'State changes and event messages are written to PostgreSQL inside the exact same database transaction, guaranteeing at-least-once delivery even during server reboots.',
    },
  },

  // --- HARDWARE & IOT WORKFLOWS ---
  {
    id: 'wf_iot_lpr',
    persona: 'iot',
    stepNumber: 'WF-19',
    title: 'LPR Camera Simulator & Gate Hardware',
    objective: 'Simulate automated entry and exit gate scans with space-scoped keys and verify plate matching.',
    actionLabel: 'Launch LPR Simulator',
    actionUrl: '/tools/lpr-simulator',
    steps: [
      {
        heading: 'Select Parking Space & Input License Plate',
        text: 'In the LPR Simulator (/tools/lpr-simulator), choose target facility and enter a vehicle plate (e.g., KA01MJ7860).',
      },
      {
        heading: 'Simulate Entry Gate Scan',
        text: 'Toggle "Entry Gate" and click "Send LPR Scan Event". The simulator POSTs to /api/iot/lpr-events with the facility camera API key. Matching active bookings immediately flip to "In Progress".',
      },
      {
        heading: 'Simulate Exit Gate Scan',
        text: 'Toggle "Exit Gate" and send the scan event. The booking duration is finalized, status transitions to "Completed", and overstay calculations execute.',
      },
      {
        heading: 'Test Blocked & Unknown Plates',
        text: 'Verify security guard behavior: scan an unregistered plate or a plate registered in the facility deny blocklist to confirm immediate "Access Denied" responses.',
      },
    ],
    callout: {
      type: 'info',
      icon: '📷',
      title: 'Real-Time Console Output',
      body: 'The LPR simulator includes a live JSON response panel showing the exact HTTP status, event payload, and gate barrier actuator command.',
    },
  },
  {
    id: 'wf_iot_ev',
    persona: 'iot',
    stepNumber: 'WF-20',
    title: 'EV Charging Station Simulator',
    objective: 'Simulate kilowatt-hour energy delivery, rate surcharges, and driver invoice line items.',
    actionLabel: 'Launch EV Simulator',
    actionUrl: '/tools/ev-charge-simulator',
    steps: [
      {
        heading: 'Select EV-Enabled Space & Vehicle',
        text: 'Open EV Simulator (/tools/ev-charge-simulator) and choose a parking space equipped with EV chargers.',
      },
      {
        heading: 'Configure Charger Speed (3.3 kW, 7.2 kW, 22 kW)',
        text: 'Select standard slow charger (3.3 kW AC), fast commercial charger (7.2 kW), or rapid DC wallbox (22 kW).',
      },
      {
        heading: 'Adjust Energy Delivered Slider',
        text: 'Drag the slider from 1 to 100 kWh to simulate the total electric power consumed by the vehicle during the stay.',
      },
      {
        heading: 'Dispatch Session Complete Event',
        text: 'Click "Simulate Session Complete". The backend calculates the electricity surcharge and appends it as an itemized line item on the driver’s final booking invoice.',
      },
    ],
    callout: {
      type: 'info',
      icon: '⚡',
      title: 'Unified Billing Experience',
      body: 'Drivers never need to swipe separate charging cards. Parking fee and electric vehicle charging are consolidated into a single Stripe checkout.',
    },
  },

  // --- SYSTEM ARCHITECTURE ---
  {
    id: 'wf_arch_engine',
    persona: 'architecture',
    stepNumber: 'WF-21',
    title: 'Technical Blueprint & Invariant Engine',
    objective: 'Review the technical foundation, caching tier, geospatial database, and security guarantees.',
    actionLabel: 'Explore System Docs',
    actionUrl: '/guide',
    steps: [
      {
        heading: 'Full-Stack Architecture Stack',
        text: 'Frontend: React 18 SPA (Vite) + Vanilla CSS tokens + Recharts. Backend: ASP.NET Core 9 Web API + Entity Framework Core. Database: PostgreSQL 16 + PostGIS for spatial queries. Cache: Upstash Redis.',
      },
      {
        heading: 'Geospatial PostGIS Indexing',
        text: 'Locations are stored as PostGIS ST_Point geography. Geospatial distance queries utilize spatial GiST indices for sub-10ms bounding box and radius queries across millions of coordinates.',
      },
      {
        heading: 'Upstash Redis Caching & Versioned Invalidation',
        text: 'Search results and listing details are cached with version tags. When a host modifies pricing or capacity, cache tags are purged immediately to prevent stale availability.',
      },
      {
        heading: 'SignalR Real-Time WebSocket Infrastructure',
        text: 'SignalR hub (/hubs/chat and /hubs/notifications) handles real-time bidirectional communication. If WebSockets drop, it falls back gracefully to Server-Sent Events (SSE) or Long Polling.',
      },
    ],
    callout: {
      type: 'architecture',
      icon: '🛡️',
      title: 'Zero-Trust Channel Isolation',
      body: 'Corporate and Marketplace JWTs have different claims and validation middleware, completely preventing cross-tenant privilege escalation.',
    },
  },
];

const BOOKING_STATES = [
  {
    code: 0,
    name: 'Pending',
    color: '#f59e0b',
    description: 'Booking created by driver; awaiting host review and approval.',
    actor: 'Driver initiated → Awaiting Host',
    nextPossible: ['Confirmed', 'Rejected', 'Cancelled', 'Expired'],
  },
  {
    code: 6,
    name: 'Awaiting Payment',
    color: '#8b5cf6',
    description: 'Host approved request; awaiting Stripe payment capture from driver.',
    actor: 'Host initiated → Awaiting Driver Payment',
    nextPossible: ['Confirmed', 'Cancelled', 'Expired'],
  },
  {
    code: 1,
    name: 'Confirmed',
    color: '#10b981',
    description: 'Payment captured and spot reserved. Driver has valid QR access pass.',
    actor: 'Stripe webhook / Host approval confirmed',
    nextPossible: ['In Progress', 'Cancelled', 'Expired'],
  },
  {
    code: 2,
    name: 'In Progress',
    color: '#6366f1',
    description: 'Driver scanned QR pass or LPR recognized plate. Actively parked.',
    actor: 'Gate Scanner / LPR Camera event',
    nextPossible: ['Completed', 'Extension Pending', 'Extension Due'],
  },
  {
    code: 8,
    name: 'Extension Pending',
    color: '#f59e0b',
    description: 'Driver requested extra parking time; awaiting host confirmation.',
    actor: 'Driver initiated → Awaiting Host review',
    nextPossible: ['Extension Due', 'In Progress', 'Completed'],
  },
  {
    code: 9,
    name: 'Extension Due',
    color: '#8b5cf6',
    description: 'Extension approved; awaiting driver incremental Stripe payment.',
    actor: 'Host approved → Awaiting Driver Payment',
    nextPossible: ['In Progress', 'Completed'],
  },
  {
    code: 3,
    name: 'Completed',
    color: '#10b981',
    description: 'Vehicle exited facility and checked out. Rating & review enabled.',
    actor: 'Exit Gate scan / Driver check-out',
    nextPossible: ['Terminal State (Review)'],
  },
  {
    code: 4,
    name: 'Cancelled',
    color: '#ef4444',
    description: 'Reservation cancelled before check-in. Refund calculated per policy.',
    actor: 'Driver or Host cancellation',
    nextPossible: ['Terminal State (Refunded)'],
  },
  {
    code: 5,
    name: 'Expired',
    color: '#64748b',
    description: 'Reservation window elapsed with no check-in recorded.',
    actor: 'Automated background cleanup worker',
    nextPossible: ['Terminal State'],
  },
  {
    code: 7,
    name: 'Rejected',
    color: '#ef4444',
    description: 'Host declined booking request with mandatory reason.',
    actor: 'Host rejected with explanation',
    nextPossible: ['Terminal State (Holds Voided)'],
  },
];

const FAQS = [
  {
    q: 'How does the Near-Me search calculate proximity to my location?',
    category: 'driver',
    a: 'When you click "Near Me", the browser requests your GPS coordinates via navigator.geolocation. High accuracy GPS coordinates (latitude, longitude) and your chosen radius (1 to 50 km) are queried in PostgreSQL using PostGIS ST_DWithin geospatial functions for sub-10ms results.',
  },
  {
    q: 'What is the IST-safe UTC Noon Date-Anchoring engine?',
    category: 'architecture',
    a: 'In day-based and multi-day bookings, Indian Standard Time (UTC+5:30) can cause dates selected at local midnight to convert to 18:30:00 UTC of the previous day, inflating inclusive day counts. ParkEase anchors day-based reservations at 12:00:00 UTC (Noon), ensuring cross-midnight calculations remain strictly accurate regardless of client browser timezone.',
  },
  {
    q: 'Why can I not book a 2-Wheeler slot with my 4-Wheeler car?',
    category: 'driver',
    a: 'ParkEase enforces strict physical inventory integrity. Parking facilities configure separate physical capacities for 4-Wheeler cars and 2-Wheeler two-wheelers. The booking engine cross-references your selected vehicle’s class with the available slot pool and blocks mismatched reservations with an explicit validation error.',
  },
  {
    q: 'What happens if I stay past my scheduled reservation time?',
    category: 'driver',
    a: 'ParkEase provides an automatic 15-minute grace period. If you need more time, you can click "Extend Booking" on your dashboard before your slot expires. If you exceed the grace period without extending, overstay notifications are dispatched and overstay surcharges are applied at check-out.',
  },
  {
    q: 'How do social logins (Google & Apple) work with ParkEase passwords?',
    category: 'driver',
    a: 'Google and Apple OAuth allow 1-click registration. If you later wish to sign in with traditional email and password, visit your Profile page (/profile) and use the "Set Password" tool. Your account will then support both social sign-in and password login seamlessly.',
  },
  {
    q: 'How do parking hosts receive payouts for completed bookings?',
    category: 'vendor',
    a: 'When a booking is completed, funds are held securely via Stripe. Host revenue accumulates in the Vendor Dashboard showing Gross Revenue, Net Payout, and Platform Fees. Hosts can configure automated weekly bank direct deposits via Stripe Connect.',
  },
  {
    q: 'What is the difference between Instant Booking and Manual Approval?',
    category: 'vendor',
    a: 'With Instant Booking enabled, drivers are confirmed immediately upon successful card checkout. With Manual Approval, the booking enters a "Pending" status and the host has a review window to approve or reject with a reason.',
  },
  {
    q: 'Can a host block off specific dates for holidays or driveway maintenance?',
    category: 'vendor',
    a: 'Yes. In your listing management settings, open "Schedule Overrides". You can select calendar date ranges where reservations are completely blocked. Alternatively, toggle your listing to "Suspended" to temporarily take it offline from search without losing historical data.',
  },
  {
    q: 'What is Corporate Channel Isolation?',
    category: 'corporate',
    a: 'ParkEase implements strict zero-trust channel isolation. Marketplace consumers and Corporate enterprise employees operate on different JWT claim channels. Corporate members access enterprise dual-pool quotas, SSO, and invoicing, while personal bookings remain completely separate.',
  },
  {
    q: 'How does the Corporate Waitlist promotion engine work?',
    category: 'corporate',
    a: 'When shared parking slots reach 100% capacity, employees can join the waitlist queue. If any employee cancels their reservation, a background worker automatically promotes the next employee in FIFO order, locks the slot, and dispatches a push notification.',
  },
  {
    q: 'How do commercial long-term leases work for corporate tenants?',
    category: 'corporate',
    a: 'Corporate administrators can browse commercial parking facilities at /corporate/lease-browse for 6, 12, or 24-month bulk agreements. Dual-pool validation guards ensure combined 4W and 2W requested spaces do not exceed the lot’s physical capacity.',
  },
  {
    q: 'Can platform administrators override corporate SSO in an emergency?',
    category: 'admin',
    a: 'Yes. In /admin/corporate-sso, Super Administrators have global oversight. If a tenant’s identity provider suffers a security breach or misconfiguration, the platform admin can apply a "Force-Disable" lock with a mandatory audit note, restoring fallback authentication.',
  },
  {
    q: 'How does the LPR camera simulator communicate with the backend?',
    category: 'iot',
    a: 'The LPR simulator sends HTTP POST requests to /api/iot/lpr-events with an Authorization header containing the facility’s camera secret key. The server validates that the key is scoped to the target space, matches the plate against confirmed bookings, and fires real-time SignalR entry/exit events.',
  },
  {
    q: 'How does the EV Charging Simulator calculate electricity surcharges?',
    category: 'iot',
    a: 'The EV simulator allows testing 3.3 kW, 7.2 kW, and 22 kW charger speeds and varying kWh power consumption. Upon session completion, the power surcharge is automatically calculated and attached as an itemized line item on the driver’s invoice.',
  },
];

const GLOSSARY = [
  {
    term: 'Dual-Pool Capacity',
    def: 'Independent allocation counters for 4-Wheeler (car) and 2-Wheeler (motorcycle) parking bays within the same physical facility, preventing slot contamination.',
  },
  {
    term: 'PostGIS ST_Point',
    def: 'Geospatial point geometry data type stored in PostgreSQL, indexed with spatial GiST trees for ultra-fast radius and bounding box searches.',
  },
  {
    term: 'LPR (License Plate Recognition)',
    def: 'Automated camera optical character recognition at gate barriers that matches incoming vehicle plates with active database bookings to lift barriers automatically.',
  },
  {
    term: 'Transactional Outbox',
    def: 'An enterprise architectural pattern where domain events are committed in the same database transaction as business entities, guaranteeing zero event loss during crashes.',
  },
  {
    term: 'IST-Safe UTC Noon Anchor',
    def: 'Anchoring day-based booking dates to 12:00:00 UTC to neutralize UTC+5:30 midnight boundary displacement across calendar dates.',
  },
  {
    term: 'Channel Isolation',
    def: 'Cryptographic separation between Marketplace and Corporate user sessions enforced via JWT audience, claims, and role authorization pipelines.',
  },
  {
    term: 'SCIM 2.0',
    def: 'System for Cross-domain Identity Management: An open protocol enabling automated user provisioning and deprovisioning between Azure AD / Okta and ParkEase.',
  },
  {
    term: 'Stripe 3DS (3D Secure)',
    def: 'Multi-factor payment authentication protocol (OTP / biometric challenge) protecting transactions against credit card fraud.',
  },
  {
    term: 'Space-Scoped Key',
    def: 'An API secret key provisioned for a physical LPR camera that is mathematically locked to a single parking lot, preventing cross-facility gate access.',
  },
  {
    term: 'Safe Return URL',
    def: 'A security guard that validates return redirect targets against an internal-only allowlist to prevent open-redirect phishing attacks.',
  },
];

// =============================================================================
// Main Component
// =============================================================================

export default function UserGuide() {
  const { isAuthenticated, user, isCorporateChannel, isAdmin } = useAuth();

  // Active persona tab
  const [activePersona, setActivePersona] = useState(() => {
    if (isAdmin) return 'admin';
    if (isCorporateChannel) return 'corporate';
    return 'driver';
  });

  // Search keyword state
  const [searchQuery, setSearchQuery] = useState('');

  // Selected State Machine item for deep inspection
  const [selectedStateCode, setSelectedStateCode] = useState(0);

  // FAQ Category filter
  const [faqCategory, setFaqCategory] = useState('all');

  // Open FAQ accordion items set
  const [openFaqIndexes, setOpenFaqIndexes] = useState(() => new Set([0]));

  // Interactive Checklist progress stored in localStorage
  const [completedSteps, setCompletedSteps] = useState(() => {
    try {
      const stored = localStorage.getItem('parkease-guide-checklist');
      return stored ? JSON.parse(stored) : [];
    } catch {
      return [];
    }
  });

  // Interactive Pricing Simulator state
  const [calcVehicleClass, setCalcVehicleClass] = useState('4W');
  const [calcPricingModel, setCalcPricingModel] = useState('hourly');
  const [calcHours, setCalcHours] = useState(3);
  const [calcDays, setCalcDays] = useState(1);
  const [calcBaseRate, setCalcBaseRate] = useState(60);

  // Toggle checklist step
  const toggleChecklistStep = (id) => {
    setCompletedSteps((prev) => {
      const next = prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id];
      try {
        localStorage.setItem('parkease-guide-checklist', JSON.stringify(next));
      } catch (e) {
        console.error('Failed to save checklist state:', e);
      }
      return next;
    });
  };

  const checklistProgressPercent = Math.round(
    (completedSteps.length / ONBOARDING_CHECKLIST_ITEMS.length) * 100
  );

  // Toggle FAQ accordion item
  const toggleFaq = (index) => {
    setOpenFaqIndexes((prev) => {
      const next = new Set(prev);
      if (next.has(index)) next.delete(index);
      else next.add(index);
      return next;
    });
  };

  // Filtered workflows based on persona & search query
  const filteredWorkflows = useMemo(() => {
    let list = WORKFLOWS;
    if (activePersona !== 'all') {
      list = list.filter((wf) => wf.persona === activePersona);
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      list = list.filter(
        (wf) =>
          wf.title.toLowerCase().includes(q) ||
          wf.objective.toLowerCase().includes(q) ||
          wf.steps.some((s) => s.heading.toLowerCase().includes(q) || s.text.toLowerCase().includes(q))
      );
    }
    return list;
  }, [activePersona, searchQuery]);

  // Filtered FAQs
  const filteredFaqs = useMemo(() => {
    let list = FAQS;
    if (faqCategory !== 'all') {
      list = list.filter((faq) => faq.category === faqCategory);
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      list = list.filter((f) => f.q.toLowerCase().includes(q) || f.a.toLowerCase().includes(q));
    }
    return list;
  }, [faqCategory, searchQuery]);

  // Pricing simulator computation
  const calcOutput = useMemo(() => {
    const classMultiplier = calcVehicleClass === '4W' ? 1.0 : 0.6; // 2W gets 40% discount
    const adjustedRate = Math.round(calcBaseRate * classMultiplier);

    let rawSubtotal = 0;
    if (calcPricingModel === 'hourly') {
      rawSubtotal = adjustedRate * Number(calcHours || 1);
    } else if (calcPricingModel === 'daily') {
      // Daily cap is roughly 8x hourly
      const dailyCap = Math.round(adjustedRate * 8);
      rawSubtotal = dailyCap * Number(calcDays || 1);
    } else {
      // Monthly pass is 22 workdays at daily cap
      const dailyCap = Math.round(adjustedRate * 8);
      rawSubtotal = Math.round(dailyCap * 20 * 0.8); // 20% bulk pass discount
    }

    const serviceFee = Math.round(rawSubtotal * 0.1); // 10% platform service fee
    const tax = Math.round((rawSubtotal + serviceFee) * 0.18); // 18% GST
    const total = rawSubtotal + serviceFee + tax;

    return {
      adjustedRate,
      rawSubtotal,
      serviceFee,
      tax,
      total,
    };
  }, [calcVehicleClass, calcPricingModel, calcHours, calcDays, calcBaseRate]);

  // Print helper
  const handlePrint = () => {
    window.print();
  };

  return (
    <div className="user-guide-page container">
      {/* -----------------------------------------------------------------------
          Hero Banner
          ----------------------------------------------------------------------- */}
      <section className="guide-hero">
        <div className="guide-hero-badge-row">
          <span className="guide-badge">📖 Official Documentation · V4.0.0</span>
          <span className="guide-badge guide-badge-success">● Verified System Specification</span>
          <span className="guide-badge guide-badge-warning">⚡ Interactive Onboarding Hub</span>
        </div>

        <h1 className="guide-hero-title">
          ParkEase Knowledge Hub &amp; Master User Guide
        </h1>
        <p className="guide-hero-subtitle">
          Your comprehensive, one-stop architectural and operational manual. Master every
          capability across <strong>Marketplace Drivers</strong>, <strong>Parking Hosts</strong>,{' '}
          <strong>Corporate Enterprises</strong>, and <strong>Platform Administrators</strong> with
          live interactive workflows, state machine explorers, and pricing calculators.
        </p>

        {/* Quick Statistics Strip */}
        <div className="guide-stats-strip">
          <div className="guide-stat-card">
            <span className="guide-stat-number">5 Core</span>
            <span className="guide-stat-label">System User Personas</span>
          </div>
          <div className="guide-stat-card">
            <span className="guide-stat-number">10 States</span>
            <span className="guide-stat-label">Booking Lifecycle Machine</span>
          </div>
          <div className="guide-stat-card">
            <span className="guide-stat-number">Dual-Pool</span>
            <span className="guide-stat-label">4W &amp; 2W Capacity Guards</span>
          </div>
          <div className="guide-stat-card">
            <span className="guide-stat-number">UTC Noon</span>
            <span className="guide-stat-label">IST-Safe Date Anchors</span>
          </div>
          <div className="guide-stat-card">
            <span className="guide-stat-number">Real-Time</span>
            <span className="guide-stat-label">SignalR WebSocket Signals</span>
          </div>
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Search & Quick Actions Toolbar
          ----------------------------------------------------------------------- */}
      <section className="guide-toolbar">
        <div className="guide-search-wrapper">
          <span className="guide-search-icon">🔍</span>
          <input
            type="text"
            className="guide-search-input"
            placeholder="Search workflows, FAQs, features, or architectural rules..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button
              type="button"
              className="guide-search-clear"
              onClick={() => setSearchQuery('')}
              title="Clear search query"
            >
              ✕
            </button>
          )}
        </div>

        <div className="guide-quick-actions">
          <button
            type="button"
            className="guide-action-btn"
            onClick={handlePrint}
            title="Print or save as PDF manual"
          >
            🖨️ Print / Save PDF
          </button>
          <a href="#quickstart" className="guide-action-btn">
            📋 5-Min Checklist
          </a>
          <a href="#notifications" className="guide-action-btn">
            🔔 Notifications
          </a>
          <a href="#statemachine" className="guide-action-btn">
            🔄 State Machine
          </a>
          <a href="#calculator" className="guide-action-btn">
            🧮 Price Simulator
          </a>
          <a href="#faqs" className="guide-action-btn guide-action-btn-primary">
            ❓ FAQ Hub
          </a>
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Interactive "Your First 5 Minutes" Checklist
          ----------------------------------------------------------------------- */}
      <section id="quickstart" className="guide-checklist-card">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>🚀</span>
            <span>Your First 5 Minutes on ParkEase — Quick Start Roadmap</span>
          </div>
          <div className="guide-badge guide-badge-success">
            {completedSteps.length} of {ONBOARDING_CHECKLIST_ITEMS.length} Completed ({checklistProgressPercent}%)
          </div>
        </div>

        <div className="guide-progress-bar-container">
          <div
            className="guide-progress-bar-fill"
            style={{ width: `${checklistProgressPercent}%` }}
          />
        </div>

        <div className="guide-checklist-grid">
          {ONBOARDING_CHECKLIST_ITEMS.map((item, idx) => {
            const isDone = completedSteps.includes(item.id);
            return (
              <div
                key={item.id}
                className={`guide-check-item ${isDone ? 'completed' : ''}`}
                onClick={() => toggleChecklistStep(item.id)}
              >
                <div className="guide-checkbox-custom">
                  {isDone ? '✓' : idx + 1}
                </div>
                <div style={{ flex: 1 }}>
                  <div className="guide-check-title">{item.title}</div>
                  <div className="guide-check-desc">{item.desc}</div>
                  <Link
                    to={item.linkTo}
                    className="guide-check-link"
                    onClick={(e) => e.stopPropagation()}
                  >
                    {item.linkText} →
                  </Link>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Interactive Notification & Overstay Lifecycle Explorer
          ----------------------------------------------------------------------- */}
      <section id="notifications" className="guide-checklist-card">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>🔔</span>
            <span>Real-Time Notifications &amp; Overstay Lifecycle Engine</span>
          </div>
          <span className="guide-badge">SignalR + Background Workers</span>
        </div>
        <p className="text-secondary" style={{ fontSize: '0.92rem', marginBottom: '1rem' }}>
          ParkEase keeps drivers and space hosts continuously aligned with instant WebSocket push notifications
          and automated background overstay monitoring.
        </p>

        <h4 style={{ color: 'var(--color-accent-light)', margin: '1rem 0 0.5rem 0' }}>
          6 Core System Notification Types
        </h4>
        <div className="guide-notifications-grid">
          {NOTIFICATION_CATEGORIES.map((cat) => (
            <div key={cat.type} className="guide-notif-card">
              <span className="guide-notif-icon">{cat.icon}</span>
              <div>
                <div className="guide-notif-type" style={{ color: cat.color }}>
                  {cat.label}
                </div>
                <div className="guide-notif-desc">{cat.desc}</div>
              </div>
            </div>
          ))}
        </div>

        <h4 style={{ color: 'var(--color-accent-light)', margin: '1.5rem 0 0.5rem 0' }}>
          Overstay Detection &amp; Session-Ending Banners
        </h4>
        <div className="guide-notifications-grid">
          {OVERSTAY_ALERTS.map((alert) => (
            <div key={alert.type} className="guide-notif-card" style={{ borderColor: alert.color }}>
              <span className="guide-notif-icon">⏱️</span>
              <div>
                <div className="guide-notif-type" style={{ color: alert.color }}>
                  {alert.badge}
                </div>
                <div className="guide-notif-desc">{alert.text}</div>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Persona Tabs Navigation
          ----------------------------------------------------------------------- */}
      <nav className="guide-tabs-nav">
        {PERSONAS.map((p) => {
          const isActive = activePersona === p.id;
          return (
            <button
              key={p.id}
              type="button"
              className={`guide-tab-btn ${isActive ? 'active' : ''}`}
              onClick={() => setActivePersona(p.id)}
            >
              <span>{p.icon}</span>
              <span>{p.name}</span>
              <span className="guide-tab-pill">{p.roleTag.split(' ')[0]}</span>
            </button>
          );
        })}
      </nav>

      {/* -----------------------------------------------------------------------
          Active Persona Banner
          ----------------------------------------------------------------------- */}
      {(() => {
        const cur = PERSONAS.find((p) => p.id === activePersona) || PERSONAS[0];
        return (
          <header className="guide-persona-header">
            <div className="guide-persona-title-row">
              <div className="guide-persona-icon-circle">{cur.icon}</div>
              <div>
                <div className="guide-badge">{cur.roleTag}</div>
                <h2 className="guide-persona-name" style={{ margin: '0.25rem 0' }}>
                  {cur.headline}
                </h2>
              </div>
            </div>
            <p className="guide-persona-description">{cur.summary}</p>
          </header>
        );
      })()}

      {/* -----------------------------------------------------------------------
          Workflows Grid for Current Persona
          ----------------------------------------------------------------------- */}
      <section className="guide-workflows-grid">
        {filteredWorkflows.length === 0 ? (
          <div className="card text-center p-3">
            <h3>No matching workflows found</h3>
            <p className="text-muted">Try clearing your search keyword or switching persona tabs.</p>
            <button
              type="button"
              className="btn btn-secondary mt-2"
              onClick={() => {
                setSearchQuery('');
                setActivePersona('driver');
              }}
            >
              Reset Filters
            </button>
          </div>
        ) : (
          filteredWorkflows.map((wf) => (
            <article key={wf.id} className="guide-workflow-card">
              <div className="guide-workflow-top">
                <div>
                  <div className="guide-workflow-step-badge">{wf.stepNumber} · Process Flow</div>
                  <h3 className="guide-workflow-title">{wf.title}</h3>
                </div>
                {wf.actionUrl && (
                  <Link to={wf.actionUrl} className="guide-workflow-action-link">
                    {wf.actionLabel} →
                  </Link>
                )}
              </div>

              <p className="guide-workflow-objective">
                <strong>Business Objective:</strong> {wf.objective}
              </p>

              <div className="guide-steps-list">
                {wf.steps.map((st, sIdx) => (
                  <div key={sIdx} className="guide-step-row">
                    <div className="guide-step-num">{sIdx + 1}</div>
                    <div className="guide-step-content">
                      <div className="guide-step-heading">{st.heading}</div>
                      <div className="guide-step-text">{st.text}</div>
                    </div>
                  </div>
                ))}
              </div>

              {wf.callout && (
                <div
                  className={`guide-callout ${
                    wf.callout.type === 'warning'
                      ? 'guide-callout-warning'
                      : wf.callout.type === 'architecture'
                      ? 'guide-callout-architecture'
                      : 'guide-callout-info'
                  }`}
                >
                  <span className="guide-callout-icon">{wf.callout.icon}</span>
                  <div className="guide-callout-body">
                    <strong>{wf.callout.title}: </strong>
                    <span>{wf.callout.body}</span>
                  </div>
                </div>
              )}
            </article>
          ))
        )}
      </section>

      {/* -----------------------------------------------------------------------
          Interactive 10-State Booking Machine Visualizer
          ----------------------------------------------------------------------- */}
      <section id="statemachine" className="guide-state-machine-container">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>🔄</span>
            <span>Interactive 10-State Booking Machine &amp; Lifecycle Explorer</span>
          </div>
          <span className="guide-badge guide-badge-warning">FSM Invariants Verified</span>
        </div>
        <p className="text-secondary" style={{ fontSize: '0.92rem', marginBottom: '1rem' }}>
          ParkEase tracks every reservation through a rigorous 10-state deterministic finite state machine.
          Click any state below to inspect trigger conditions, responsible actors, and allowable next transitions.
        </p>

        <div className="guide-states-grid">
          {BOOKING_STATES.map((st) => {
            const isSelected = selectedStateCode === st.code;
            return (
              <button
                key={st.code}
                type="button"
                className={`guide-state-pill-btn ${isSelected ? 'active' : ''}`}
                onClick={() => setSelectedStateCode(st.code)}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <span
                    style={{
                      width: '10px',
                      height: '10px',
                      borderRadius: '50%',
                      background: st.color,
                      display: 'inline-block',
                    }}
                  />
                  <span style={{ fontWeight: 600 }}>{st.name}</span>
                </div>
                <span className="guide-state-code">Code {st.code}</span>
              </button>
            );
          })}
        </div>

        {(() => {
          const stateDetail =
            BOOKING_STATES.find((s) => s.code === selectedStateCode) || BOOKING_STATES[0];
          return (
            <div className="guide-state-detail-box">
              <div className="guide-state-detail-title">
                <span
                  style={{
                    width: '12px',
                    height: '12px',
                    borderRadius: '50%',
                    background: stateDetail.color,
                  }}
                />
                <span>
                  State {stateDetail.code}: {stateDetail.name}
                </span>
              </div>
              <p style={{ margin: '0.5rem 0', color: 'var(--color-text-primary)' }}>
                {stateDetail.description}
              </p>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '1.5rem', marginTop: '1rem', fontSize: '0.88rem' }}>
                <div>
                  <strong style={{ color: 'var(--color-text-secondary)' }}>Responsible Actor:</strong>{' '}
                  <span>{stateDetail.actor}</span>
                </div>
                <div>
                  <strong style={{ color: 'var(--color-text-secondary)' }}>Valid Outgoing Transitions:</strong>{' '}
                  <span style={{ color: 'var(--color-accent-light)', fontWeight: 600 }}>
                    {stateDetail.nextPossible.join('  ➜  ')}
                  </span>
                </div>
              </div>
            </div>
          );
        })()}
      </section>

      {/* -----------------------------------------------------------------------
          Interactive Pricing Simulator Widget
          ----------------------------------------------------------------------- */}
      <section id="calculator" className="guide-pricing-sim-card">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>🧮</span>
            <span>Interactive Fare &amp; Extension Pricing Engine Simulator</span>
          </div>
          <span className="guide-badge">Live Client Computation</span>
        </div>
        <p className="text-secondary" style={{ fontSize: '0.92rem', marginBottom: '1rem' }}>
          Simulate how ParkEase transparently calculates fares, discounts 2-Wheelers, caps daily maximums,
          and calculates the 10% Platform Service Fee + 18% GST (Tax).
        </p>

        <div className="guide-calc-grid">
          {/* Controls */}
          <div className="guide-calc-controls">
            <div className="form-group">
              <label className="form-label">Vehicle Class Pool:</label>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <button
                  type="button"
                  className={`btn ${calcVehicleClass === '4W' ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ flex: 1 }}
                  onClick={() => setCalcVehicleClass('4W')}
                >
                  🚗 4-Wheeler (Car/SUV)
                </button>
                <button
                  type="button"
                  className={`btn ${calcVehicleClass === '2W' ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ flex: 1 }}
                  onClick={() => setCalcVehicleClass('2W')}
                >
                  🛵 2-Wheeler (Bike/Scooter)
                </button>
              </div>
            </div>

            <div className="form-group">
              <label className="form-label">Duration Pricing Model:</label>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <button
                  type="button"
                  className={`btn ${calcPricingModel === 'hourly' ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ flex: 1 }}
                  onClick={() => setCalcPricingModel('hourly')}
                >
                  Hourly Rate
                </button>
                <button
                  type="button"
                  className={`btn ${calcPricingModel === 'daily' ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ flex: 1 }}
                  onClick={() => setCalcPricingModel('daily')}
                >
                  Daily Max Cap
                </button>
                <button
                  type="button"
                  className={`btn ${calcPricingModel === 'monthly' ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ flex: 1 }}
                  onClick={() => setCalcPricingModel('monthly')}
                >
                  Monthly Pass
                </button>
              </div>
            </div>

            {calcPricingModel === 'hourly' && (
              <div className="form-group">
                <label className="form-label">Duration in Hours: {calcHours} hrs</label>
                <input
                  type="range"
                  min="1"
                  max="24"
                  value={calcHours}
                  onChange={(e) => setCalcHours(Number(e.target.value))}
                  style={{ width: '100%' }}
                />
              </div>
            )}

            {calcPricingModel === 'daily' && (
              <div className="form-group">
                <label className="form-label">Duration in Days: {calcDays} days</label>
                <input
                  type="range"
                  min="1"
                  max="30"
                  value={calcDays}
                  onChange={(e) => setCalcDays(Number(e.target.value))}
                  style={{ width: '100%' }}
                />
              </div>
            )}

            <div className="form-group">
              <label className="form-label">Base Hourly Benchmark Rate (₹):</label>
              <input
                type="number"
                className="form-input"
                min="20"
                max="500"
                value={calcBaseRate}
                onChange={(e) => setCalcBaseRate(Math.max(20, Number(e.target.value)))}
              />
            </div>
          </div>

          {/* Fare Breakdown Output */}
          <div className="guide-calc-output">
            <div>
              <h4 style={{ color: 'var(--color-accent-light)', marginBottom: '1rem' }}>
                Itemized Fare Breakdown
              </h4>

              <div className="guide-calc-line-item">
                <span>Effective Rate:</span>
                <span>₹{calcOutput.adjustedRate}/hr {calcVehicleClass === '2W' ? '(2W Discounted)' : ''}</span>
              </div>

              <div className="guide-calc-line-item">
                <span>Base Subtotal:</span>
                <span>₹{calcOutput.rawSubtotal}</span>
              </div>

              <div className="guide-calc-line-item">
                <span>Platform Service Fee (10%):</span>
                <span>₹{calcOutput.serviceFee}</span>
              </div>

              <div className="guide-calc-line-item">
                <span>GST Tax (18%):</span>
                <span>₹{calcOutput.tax}</span>
              </div>
            </div>

            <div className="guide-calc-total-row">
              <span>Total Payable Amount:</span>
              <span className="guide-calc-total-val">₹{calcOutput.total}</span>
            </div>
          </div>
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          System Architecture & Technical Blueprint
          ----------------------------------------------------------------------- */}
      <section className="guide-arch-container">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>📐</span>
            <span>Enterprise System Architecture &amp; Data Pipeline Blueprint</span>
          </div>
          <span className="guide-badge guide-badge-success">ISO/IEC/IEEE 29119 Compliant</span>
        </div>
        <p className="text-secondary" style={{ fontSize: '0.92rem', marginBottom: '1rem' }}>
          Engineered for 99.99% availability, zero race conditions on slot booking, sub-10ms PostGIS queries,
          and robust zero-data-loss transactional outbox event guarantees.
        </p>

        <div className="guide-arch-grid">
          <div className="guide-arch-layer">
            <div className="guide-arch-layer-title">
              <span>🖥️</span> Presentation Layer
            </div>
            <ul className="guide-arch-item-list">
              <li className="guide-arch-item">React 18 SPA (Vite)</li>
              <li className="guide-arch-item">Dual Channel Isolation (Marketplace vs Corp)</li>
              <li className="guide-arch-item">Leaflet + PostGIS Map Clusters</li>
              <li className="guide-arch-item">Adaptive Light/Dark CSS Variable Design System</li>
            </ul>
          </div>

          <div className="guide-arch-layer">
            <div className="guide-arch-layer-title">
              <span>⚙️</span> API &amp; Domain Engine
            </div>
            <ul className="guide-arch-item-list">
              <li className="guide-arch-item">ASP.NET Core 9 Web API Engine</li>
              <li className="guide-arch-item">Deterministic 10-State Booking Machine</li>
              <li className="guide-arch-item">IST-Safe UTC Noon Date-Anchoring</li>
              <li className="guide-arch-item">ML.NET Occupancy Forecasting Engine</li>
            </ul>
          </div>

          <div className="guide-arch-layer">
            <div className="guide-arch-layer-title">
              <span>💾</span> Data &amp; Caching Tier
            </div>
            <ul className="guide-arch-item-list">
              <li className="guide-arch-item">PostgreSQL 16 + PostGIS Spatial Engine</li>
              <li className="guide-arch-item">Upstash Redis Multi-Tier Cache &amp; Versioning</li>
              <li className="guide-arch-item">Transactional Outbox Event Tables</li>
              <li className="guide-arch-item">Cloudflare R2 Direct Pre-Signed Uploads</li>
            </ul>
          </div>

          <div className="guide-arch-layer">
            <div className="guide-arch-layer-title">
              <span>🔌</span> Integrations &amp; IoT
            </div>
            <ul className="guide-arch-item-list">
              <li className="guide-arch-item">Stripe Elements + 3DS Webhook Engine</li>
              <li className="guide-arch-item">SignalR WebSockets Real-Time Delivery</li>
              <li className="guide-arch-item">SAML 2.0 / OIDC Enterprise SSO &amp; SCIM</li>
              <li className="guide-arch-item">Space-Scoped LPR Gate Camera APIs</li>
            </ul>
          </div>
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Searchable FAQ Accordion
          ----------------------------------------------------------------------- */}
      <section id="faqs" className="guide-faq-section">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>❓</span>
            <span>Frequently Asked Questions &amp; Troubleshooting Hub</span>
          </div>
          <span className="guide-badge">{filteredFaqs.length} Answers Available</span>
        </div>

        {/* Filter buttons */}
        <div className="guide-faq-filter-row">
          {['all', 'driver', 'vendor', 'corporate', 'admin', 'iot', 'architecture'].map((cat) => (
            <button
              key={cat}
              type="button"
              className={`guide-faq-filter-btn ${faqCategory === cat ? 'active' : ''}`}
              onClick={() => setFaqCategory(cat)}
            >
              {cat === 'all'
                ? 'All Categories'
                : cat === 'driver'
                ? '🚗 Drivers'
                : cat === 'vendor'
                ? '🅿️ Hosts'
                : cat === 'corporate'
                ? '🏢 Corporate'
                : cat === 'admin'
                ? '🛡️ Admins'
                : cat === 'iot'
                ? '⚡ Hardware'
                : '📐 Architecture'}
            </button>
          ))}
        </div>

        <div className="guide-faq-list">
          {filteredFaqs.map((faq, idx) => {
            const isOpen = openFaqIndexes.has(idx);
            return (
              <div key={idx} className="guide-faq-item">
                <button
                  type="button"
                  className="guide-faq-question-btn"
                  onClick={() => toggleFaq(idx)}
                >
                  <span>{faq.q}</span>
                  <span className={`guide-faq-arrow ${isOpen ? 'open' : ''}`}>▼</span>
                </button>
                {isOpen && <div className="guide-faq-answer">{faq.a}</div>}
              </div>
            );
          })}
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Glossary of Key Terms
          ----------------------------------------------------------------------- */}
      <section className="guide-checklist-card">
        <div className="guide-checklist-header">
          <div className="guide-checklist-title">
            <span>📚</span>
            <span>System Glossary &amp; Technical Concepts</span>
          </div>
          <span className="guide-badge">Key Terms</span>
        </div>

        <div className="guide-glossary-grid">
          {GLOSSARY.map((term, tIdx) => (
            <div key={tIdx} className="guide-glossary-card">
              <div className="guide-glossary-term">{term.term}</div>
              <div className="guide-glossary-def">{term.def}</div>
            </div>
          ))}
        </div>
      </section>

      {/* -----------------------------------------------------------------------
          Bottom Call to Action Banner
          ----------------------------------------------------------------------- */}
      <div className="card text-center p-3 mt-3">
        <h3 style={{ fontSize: '1.6rem', marginBottom: '0.75rem' }}>
          Ready to experience the future of smart parking?
        </h3>
        <p className="text-secondary" style={{ maxWidth: '650px', margin: '0 auto 1.5rem' }}>
          Whether you need instant hourly parking, want to monetize your driveway, or need enterprise
          workplace parking with SAML SSO, ParkEase is ready.
        </p>
        <div className="flex-center gap-2 flex-wrap">
          <Link to="/search" className="btn btn-primary">
            🔍 Find Parking Now
          </Link>
          <Link to="/my/listings" className="btn btn-secondary">
            🅿️ Host Your Space
          </Link>
          <Link to="/corporate/dashboard" className="btn btn-outline">
            🏢 Enterprise Portal
          </Link>
        </div>
      </div>
    </div>
  );
}
