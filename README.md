# ParkEase - Smart Parking Management System

A full-stack parking space management application built with **.NET 9 Web API** and **React**. Enables property owners to list parking spaces and users to discover, book, and pay for parking in real-time.

## 🚀 Features

### For Users (Members)
- 🔍 Search parking spaces by location, date, and vehicle type
- 📅 Book parking with flexible hourly/daily/monthly pricing
- 💳 Secure payment processing
- 🔔 Real-time notifications for booking updates
- ⭐ Rate and review parking spaces

### For Vendors (Parking Owners)
- 📍 List and manage parking spaces
- 📸 Upload images and videos of parking spots
- ✅ Approve/reject booking requests
- 📊 Dashboard with booking analytics
- 🔔 Real-time alerts for new bookings and payments

### Technical Highlights
- 🏗️ **Clean Architecture** with Domain-Driven Design (DDD)
- 📨 **CQRS Pattern** for command/query separation
- ⚡ **SignalR** for real-time notifications
- 🔐 **JWT Authentication** with role-based authorization
- 🛡️ Security headers, rate limiting, and input validation

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| **Backend** | .NET 9, ASP.NET Core Web API |
| **Frontend** | React 18, Vite, Axios |
| **Database** | SQLite (dev) / SQL Server (prod) |
| **Real-time** | SignalR |
| **Auth** | JWT Bearer Tokens |
| **API Gateway** | YARP (Yet Another Reverse Proxy) |

---

## 📁 Project Structure

```
ParkingApp/
├── backend/
│   └── src/
│       ├── ParkingApp.API/           # Controllers, middleware
│       ├── ParkingApp.Application/   # Services, DTOs, CQRS
│       ├── ParkingApp.Domain/        # Entities, interfaces
│       ├── ParkingApp.Infrastructure/# EF Core, repositories
│       └── ParkingApp.Gateway/       # YARP API Gateway
├── frontend/
│   └── src/
│       ├── pages/                    # React page components
│       ├── contexts/                 # Auth context
│       ├── services/                 # API service layer
│       └── hooks/                    # Custom hooks (SignalR)
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

# Restore packages
dotnet restore

# Run the API (default: http://localhost:5129)
dotnet run --project src/ParkingApp.API
```

The API will start at `http://localhost:5129`. The database (SQLite) is auto-created on first run.

### 3. Run the Frontend

Open a new terminal:

```bash
cd frontend

# Install dependencies
npm install

# Start dev server (default: http://localhost:5173)
npm run dev
```

### 4. Access the Application

| URL | Description |
|-----|-------------|
| http://localhost:5173 | Frontend application |
| http://localhost:5129/api | Backend API |
| http://localhost:5129/health | Health check endpoint |

---

## 🔐 Default Test Accounts

After first run, you can register new accounts or use the seeded data.

| Role | Email | Password |
|------|-------|----------|
| Member | Register via UI | - |
| Vendor | Register via UI (select "List your parking") | - |

---

## 📡 API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login and get JWT token

### Parking Spaces
- `GET /api/parking` - Search parking spaces
- `GET /api/parking/{id}` - Get parking details
- `POST /api/parking` - Create listing (Vendor)

### Bookings
- `POST /api/bookings` - Create booking
- `GET /api/bookings/my-bookings` - Get user's bookings
- `POST /api/bookings/{id}/approve` - Approve booking (Vendor)
- `POST /api/bookings/{id}/reject` - Reject booking (Vendor)

### Payments
- `POST /api/payments` - Process payment

---

## 🔔 Real-Time Notifications

SignalR hub endpoint: `ws://localhost:5129/hubs/notifications`

| Event | Triggered When |
|-------|----------------|
| `booking.requested` | New booking created |
| `booking.approved` | Vendor approves booking |
| `booking.rejected` | Vendor rejects booking |
| `booking.cancelled` | Booking cancelled |
| `payment.completed` | Payment successful |
| `booking.checkin` | User checks in |

---

## 🧪 Development

### Build Frontend for Production

```bash
cd frontend
npm run build
```

### Build Backend

```bash
cd backend
dotnet build
```

### Run Tests

```bash
cd backend
dotnet test
```

---

## 📝 Environment Variables

### Frontend (.env)
```env
VITE_API_URL=http://localhost:5129
```

### Backend (appsettings.json)
```json
{
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "ParkingApp",
    "Audience": "ParkingApp"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=parking.db"
  }
}
```

---

## 📄 License

This project is for educational purposes.

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request