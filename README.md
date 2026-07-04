# ParkEase - Smart Parking Management System

A full-stack parking space management application built with **.NET 9 Web API** and **React**. Enables users to host parking spaces, discover spots, book, pay, and forecast likely parking availability in real-time.

## 🚀 Features

### For Users (Unified Account)
- 📅 **Flexible Booking**: Reserve parking with hourly, daily, or monthly pricing models.
- ⏳ **Booking Extensions**: Seamlessly request to extend an active booking (requires owner approval).
- 🚗 **My Garage**: Save and manage multiple vehicles (License Plate, Make, Model, Color) for faster checkout.
- ❤️ **Favorites**: Save and quickly access your most-used parking locations.
- 📍 **Advanced Search**: Discover parking by location, radius, date range, vehicle type, and specific amenities.
- 🗺️ **Interactive Map View**: Visualize parking locations with real-time availability and distance estimates using **Leaflet** and **OSRM**.
- 💬 **Direct Messaging**: Chat in real-time with parking owners to coordinate arrivals or ask questions.
- 🔔 **Notification Center**: Manage real-time alerts and FCM push notifications for booking updates, payment requests, and system alerts.
- ⭐ **Reviews & Ratings**: Share experiences with multi-media reviews, helpfulness votes, and host interactions.
- 💳 **Secure Payments**: Integrated with **Stripe** for one-time payments and booking extensions.
- 🏢 **Host Parking Spaces**: Effortlessly list your own parking spaces with detailed descriptions, pricing, and rules.
- 🔌 **Availability Toggle**: Quickly enable or disable your listings to manage temporary closures.
- 📸 **Rich Media**: Upload high-quality bucket-stored images and videos for parking spots.
- ✅ **Host Operations**: Review, approve, or reject booking and extension requests in real-time.
- 📊 **Host Analytics**: Interactive dashboard with revenue charts (Area) and booking volume (Bar) using **Recharts**.
- 📟 **Occupancy Tracking**: Real-time view of current bookings with manual check-in/out capabilities.

- **Parking Availability Prediction Engine**: Forecast likely free spots, occupancy risk, confidence scores, and upcoming tight/full windows for your hosted listings.

### Technical Highlights
- **Custom CQRS Implementation**: Manually registered commands and queries with vertical slice handlers instead of a third-party mediator stack.
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
| **Push Notifications**| Firebase Cloud Messaging (FCM) |
| **Auth** | JWT Bearer Tokens |
| **Maps & Geo** | Leaflet, NetTopologySuite, OSRM (Distance API) |
| **Forecasting** | In-house hybrid prediction engine (deterministic logic + ML.NET) |
| **Gateway** | YARP (Yet Another Reverse Proxy) |

## Parking Availability Prediction

- Built on the existing Clean Architecture, DDD, and custom CQRS setup.
- Uses a hybrid forecasting service that combines deterministic booking math with an ML.NET regression model trained from historical occupancy patterns.
- The deterministic layer preserves hard business constraints such as active bookings, cancellation handling, listing status, and total spot capacity.
- The ML layer adds pattern recognition for weekday/hour trends, recent occupancy momentum, pricing, ratings, and listing characteristics without introducing any paid dependencies.
- Returns forecast buckets with likely booked spots, likely available spots, predicted occupancy rate, confidence score, and availability bands.
- Keeps server storage overhead low by caching a shared in-memory model per forecast interval instead of generating separate model files per listing or user.
- Powers the `My Listings` experience so the same unified user account can act as host and see near-term availability risk for hosted spaces.

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
| User | Register via UI | - |

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
- `POST /api/parking` - Create new listing (Owner)
- `POST /api/parking/{id}/toggle-active` - Enable/Disable listing (Owner)
- `GET /api/parking-availability/{parkingSpaceId}/forecast` - Get forecast buckets for a parking space
- `GET /api/parking-availability/my-listings` - Get forecast summaries for the current user's hosted listings

### Bookings (V2)
- `POST /api/v2/bookings` - Create reservation
- `GET /api/v2/bookings/my-bookings` - Filtered history for your bookings
- `GET /api/v2/bookings/vendor-bookings` - Management portal for your listed spaces
- `POST /api/v2/bookings/{id}/extend` - Request booking extension
- `POST /api/v2/bookings/{id}/approve-extension` - Approve extension (Owner)
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
Push Notifications: Firebase Cloud Messaging (FCM)

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
