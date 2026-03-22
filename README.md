# ParkEase - Smart Parking Management System

A full-stack parking space management application built with **.NET 9 Web API** and **React**. Enables property owners to list parking spaces and users to discover, book, and pay for parking in real-time.

## 🚀 Features

### For Users (Members)
- 📅 **Flexible Booking**: Reserve parking with hourly, daily, or monthly pricing models.
- ⏳ **Booking Extensions**: Seamlessly request to extend an active booking (requires vendor approval).
- 🚗 **My Garage**: Save and manage multiple vehicles (License Plate, Make, Model, Color) for faster checkout.
- ❤️ **Favorites**: Save and quickly access your most-used parking locations.
- 📍 **Advanced Search**: Discover parking by location, radius, date range, vehicle type, and specific amenities.
- 🗺️ **Interactive Map View**: Visualize parking locations with real-time availability and distance estimates using **Leaflet** and **OSRM**.
- 💬 **Direct Messaging**: Chat in real-time with parking owners to coordinate arrivals or ask questions.
- 🔔 **Notification Center**: Manage real-time alerts for booking updates, payment requests, and system alerts.
- ⭐ **Reviews & Ratings**: Share experiences with multi-media reviews, helpfulness votes, and vendor interactions.
- 💳 **Secure Payments**: Integrated with **Stripe** for one-time payments and booking extensions.

### For Vendors (Parking Owners)
- 🏢 **Listing Management**: Effortlessly list parking spaces with detailed descriptions, pricing, and rules.
- 🔌 **Availability Toggle**: Quickly enable or disable listings to manage temporary closures.
- 📸 **Rich Media**: Upload high-quality bucket-stored images and videos for parking spots.
- ✅ **Booking Operations**: Review, approve, or reject booking and extension requests in real-time.
- 📊 **Advanced Analytics**: Interactive dashboard with revenue charts (Area) and booking volume (Bar) using **Recharts**.
- 📟 **Occupancy Tracking**: Real-time view of current bookings with manual check-in/out capabilities.
- 💬 **Vendor Chat**: Communication portal to manage user inquiries directly from the dashboard.
- 🔔 **Real-Time Notifications**: Instant SignalR-driven alerts for new requests and successful payments.

### Technical Highlights
- 🏗️ **Clean Architecture**: Domain-Driven Design (DDD) with a clear separation of concerns.
- 📨 **CQRS Pattern**: Decoupled commands and queries for high performance and scalability.
- ⚡ **SignalR Integration**: Robust real-time engine with automatic reconnection and "silent" UI update support.
- 🛠️ **100% Domain Test Coverage**: Full unit test suite ensuring core logic reliability.
- 🗺️ **Spatial Intelligence**: Geo-spatial indexing and distance calculations using **NetTopologySuite**.
- 🛡️ **API Security**: JWT Bearer authentication, role-based access control, rate limiting, and YARP API Gateway.

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| **Backend** | .NET 9, ASP.NET Core Web API, EF Core |
| **Frontend** | React 18, Vite, Recharts, Axios |
| **Database** | SQLite (Dev) / SQL Server (Prod) |
| **Real-time** | SignalR (WebSockets) |
| **Auth** | JWT Bearer Tokens |
| **Maps & Geo** | Leaflet, NetTopologySuite, OSRM (Distance API) |
| **Gateway** | YARP (Yet Another Reverse Proxy) |

---

## 📁 Project Structure

```
ParkingApp/
├── backend/
│   └── src/
│       ├── ParkingApp.API/           # Controllers, Middlewares
│       ├── ParkingApp.Application/   # Services, DTOs, CQRS (Cmds/Queries)
│       ├── ParkingApp.Domain/        # Entities, Enums, Value Objects, Logic
│       ├── ParkingApp.Infrastructure/# EF Core, Repositories, External Services
│       └── ParkingApp.Gateway/       # YARP API Gateway & Rate Limiting
├── frontend/
│   └── src/
│       ├── pages/                    # Main page components
│       ├── components/               # Reusable UI (Dropdowns, Modals)
│       ├── contexts/                 # Auth & Notification providers
│       ├── services/                 # API service layer (Axios/Fetch)
│       └── hooks/                    # Custom hooks (SignalR, Real-time)
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- Git

### 1. Clone the Repository

```bash
git clone <repository-url>
cd ParkingApp
```

### 2. Run the Backend

```bash
cd backend
dotnet restore
dotnet run --project src/ParkingApp.API
```

### 3. Run the Frontend

```bash
cd frontend
npm install
npm run dev
```

---

## 🔐 Default Test Accounts

| Role | Email | Password |
|------|-------|----------|
| Member | Register via UI | - |
| Vendor | Register via UI (select "List your parking") | - |

---

## 📡 API Endpoints (V2 Highlight)

### Authentication & Users
- `POST /api/auth/register` - New user signup
- `POST /api/auth/login` - Authenticate and receive JWT
- `GET /api/users/me` - Profile overview
- `PUT /api/users/me` - Update profile details

### Parking & Search
- `GET /api/parking/search` - Advanced search (radius, pricing, amenities)
- `GET /api/parking/map` - Geo-coordinates for map visualization
- `GET /api/parking/{id}` - Comprehensive parking details
- `POST /api/parking` - Create new listing (Vendor)
- `POST /api/parking/{id}/toggle-active` - Enable/Disable listing (Vendor)

### Bookings (V2)
- `POST /api/v2/bookings` - Create reservation
- `GET /api/v2/bookings/my-bookings` - Filtered history for members
- `GET /api/v2/bookings/vendor-bookings` - Management portal for vendors
- `POST /api/v2/bookings/{id}/extend` - Request booking extension
- `POST /api/v2/bookings/{id}/approve-extension` - Approve extension (Vendor)
- `POST /api/v2/bookings/{id}/check-in` - Manual check-in
- `POST /api/v2/bookings/{id}/check-out` - Manual check-out

### Communication & Social
- `GET /api/chat/conversations` - List active message threads
- `POST /api/chat/send` - Send real-time message
- `POST /api/favorites/{id}/toggle` - Save/Unsave parking spot
- `POST /api/reviews` - Submit review with rating

### Notifications
- `GET /api/notifications` - Paginated alert history
- `PUT /api/notifications/read-all` - Clear unread badges
- `DELETE /api/notifications/clear-all` - Remove all user alerts

---

## 🔔 Real-Time Event Engine

SignalR hub: `ws://localhost:5129/hubs/notifications`

| Event | Status |
|-------|--------|
| `booking.requested` | 📥 Pending |
| `booking.approved` | ✅ Confirmed |
| `booking.extension_requested` | ⏳ Pending Review |
| `payment.completed` | 💰 Paid |
| `booking.checkin` | 🚗 In Progress |
| `chat.message_received` | 💬 New Message |

---

## 📄 License

This project is for educational purposes.
