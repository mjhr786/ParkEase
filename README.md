# ParkEase - Smart Parking Management System

A full-stack parking management platform built with **.NET 9 Web API**, **React**, and **React Native (Expo)**. Users can host parking spaces, discover spots, book, pay, chat, manage corporate fleets, and forecast availability—backed by production-grade caching, transactional outbox, and Clean Architecture.

## Features

### Marketplace (unified account)
- **Flexible booking** — hourly / daily / monthly pricing; owner approval workflow
- **Booking extensions** — request extra time; owner approve/reject; payment when required
- **Check-in / check-out** — occupancy tracking for hosts
- **My Garage** — multiple vehicles for faster checkout
- **Favorites** — pin frequently used locations
- **Advanced search** — location, radius, price, vehicle type, amenities
- **Interactive map** — Leaflet + OSRM distance/duration
- **Direct messaging** — SignalR chat with hosts
- **Notifications** — in-app + FCM push (booking, payment, system)
- **Reviews & ratings** — multi-media reviews and owner responses
- **Payments** — Stripe integration for bookings and extensions
- **Parking passes** — personal and corporate passes with usage policies
- **Host dashboard** — revenue charts, pending requests, listing analytics
- **Media** — Cloudflare R2 (S3-compatible) object storage
- **Availability prediction** — hybrid deterministic + ML.NET occupancy forecasts

### Corporate parking
- **Companies** — create/manage org, members, invitations
- **Allocations** — leased or company-owned slots, fixed vs shared, booking policies
- **Employee & visitor bookings** — quota-aware reservation + fraud checks
- **Waitlist** — auto-promotion when shared slots free up
- **Invoices** — manual period generate, issue, offline mark paid, void, CSV export
- **Company dashboard** — aggregates for admins
- **Quota cache** — allocation snapshots cached and invalidated on policy changes

### Technical highlights
- **Clean Architecture + DDD** — Domain, Application, Infrastructure, API, Notifications
- **Custom CQRS** — dispatcher, pipeline behaviors (logging, transactions, validation)—no MediatR
- **Transactional outbox** — reliable domain-event dispatch after commit
- **Redis / Upstash cache** — distributed cache with in-memory fallback; versioned invalidation
- **Accurate cache invalidation** — centralized `CacheKeys` + `CacheInvalidation` on all critical mutations
- **PostgreSQL + PostGIS** — geo queries via NetTopologySuite / Npgsql
- **Dapper read models** — search, map, dashboards, corporate reads
- **SignalR** — notifications + chat hubs
- **Serilog** — structured console + rolling file logs
- **JWT auth** — access + refresh tokens, role-based access

---

## Tech stack

| Layer | Technology |
|-------|------------|
| **Backend** | .NET 9, ASP.NET Core Web API, EF Core 9 |
| **Frontend** | React 18, Vite, Recharts, Axios |
| **Mobile** | React Native (Expo), Redux Toolkit |
| **Database** | PostgreSQL + PostGIS (Supabase-compatible connection strings) |
| **Cache** | **Upstash Redis** (`rediss://`) via StackExchange.Redis; in-memory fallback |
| **Object storage** | Cloudflare R2 (AWS S3 SDK) |
| **Payments** | Stripe |
| **Email** | Resend |
| **Push** | Firebase Cloud Messaging (FCM) |
| **Real-time** | SignalR |
| **Maps / geo** | Leaflet, NetTopologySuite, OSRM |
| **Forecasting** | Hybrid engine (booking math + ML.NET) |
| **Gateway** | YARP (optional reverse proxy) |
| **Logging** | Serilog |

---

## Architecture

```
ParkEase/
├── backend/
│   └── src/
│       ├── ParkingApp.API/             # Controllers, middleware, SPA host, JWT
│       ├── ParkingApp.Application/     # CQRS handlers, DTOs, CacheKeys, services
│       ├── ParkingApp.Domain/          # Aggregates, VOs, domain events, rules
│       ├── ParkingApp.Infrastructure/  # EF, Dapper, Redis, outbox, Stripe, R2, ML
│       ├── ParkingApp.Notifications/   # SignalR hubs, FCM coordination
│       ├── ParkingApp.BuildingBlocks/  # Result, guards, exceptions, logging helpers
│       └── ParkingApp.Gateway/         # YARP reverse proxy (optional)
│   └── tests/
│       └── ParkingApp.UnitTests/
├── frontend/                           # React web app
└── Mobile/                             # React Native (Expo)
```

**Dependency rule:** Domain has no knowledge of EF, HTTP, Redis, or Stripe. Infrastructure implements Application ports. Controllers only dispatch commands/queries.

Vertical slices under Application CQRS:
- `Marketplace/` — parking, bookings, payments, reviews, passes, favorites
- `Identity/` — auth, users, vehicles, device tokens
- `Corporate/` — companies, allocations, corporate bookings, waitlist
- `Messaging/` — chat, notifications
- `Caching/` — `CacheKeys`, `CacheInvalidation`

---

## Caching (Redis / Upstash)

ParkEase uses a production-grade distributed cache. **Local Development** uses Docker Redis; **Production** uses Upstash (`rediss://`).

### Runtime selection
| Environment | Source | Implementation |
|-------------|--------|----------------|
| Development | `appsettings.Development.json` → local Docker Redis | `RedisCacheService` |
| Production | `appsettings.Production.json` → Upstash | `RedisCacheService` |
| Missing / placeholder | `SET_VIA_USER_SECRETS_OR_ENV_VAR` / empty | `InMemoryCacheService` |

Startup logs:
```text
>> Using REDIS Cache (local Docker, instance=ParkEase_Local_)
>> Using REDIS Cache (Upstash, instance=ParkEase_Prod_)
# or
>> Using IN-MEMORY Cache (Redis not configured)
```

### Local Docker Redis

From repo root:

```bash
cd Redis
docker compose up -d
```

Compose maps `localhost:6379` with password `DevRedis@123` (see `Redis/docker-compose.yml`).

**`appsettings.Development.json`** (applied when `ASPNETCORE_ENVIRONMENT=Development`):

```json
"ConnectionStrings": {
  "Redis": "localhost:6379,password=DevRedis@123,abortConnect=false"
},
"Redis": {
  "InstanceName": "ParkEase_Local_"
}
```

Bare `localhost:6379` (no password) is treated as unconfigured so the API falls back to in-memory cache.

### Configuration

**`appsettings.json` (non-secret defaults / placeholders):**

```json
"ConnectionStrings": {
  "DefaultConnection": "SET_VIA_USER_SECRETS_OR_ENV_VAR",
  "Redis": "SET_VIA_USER_SECRETS_OR_ENV_VAR"
},
"Redis": {
  "InstanceName": "ParkEase_Dev_",
  "DefaultTtlMinutes": 15,
  "MaxTtlMinutes": 60,
  "CompressionThresholdBytes": 256,
  "ConnectTimeoutMs": 5000,
  "SyncTimeoutMs": 5000,
  "AsyncTimeoutMs": 5000,
  "ConnectRetry": 3,
  "KeepAliveSeconds": 60,
  "VersionedNamespaces": [ "search", "map", "parking-forecast", "owner-parking-forecast" ]
}
```

**Production:** `appsettings.Production.json` (or host env vars) should set:
- `ConnectionStrings:Redis` — Upstash `rediss://` URL  
- `Redis:InstanceName` — e.g. `ParkEase_Prod_` (isolates keys per environment)

Optional override via user secrets / env (any environment):

```bash
cd backend/src/ParkingApp.API
dotnet user-secrets set "ConnectionStrings:Redis" "rediss://default:<TOKEN>@<host>.upstash.io:6379"
```

Upstash console DB name: **ParkEase**. The DB name is a label; connectivity uses host + TLS + password.

### Production optimizations (`RedisCacheService`)
- Proper **`rediss://` URL parsing** (StackExchange.Redis `Parse` alone mishandles Upstash URIs)
- TLS 1.2/1.3, `AbortOnConnectFail=false`, reconnect + timeouts
- **Single-round-trip SET + EX** (no separate EXPIRE)
- Default/max TTL so keys cannot live forever on free-tier storage
- **GZip** for JSON payloads above threshold
- **Version-stamp invalidation** for high-churn namespaces (`search:*`, `map:*`, forecasts) — one `INCR` instead of KEYS/SCAN
- Always read version from Redis (no process-local version cache) so multi-instance invalidation is immediate
- Safe distributed locks (unique token + Lua release)
- Fail-open: Redis errors log and degrade to cache-miss / no-op so the API stays up

### Cached hot paths

| Data | Example key | Typical TTL |
|------|-------------|-------------|
| Parking detail (+ reservations) | `parking:{id}` | 5m |
| Search results | `search:…` | 2m |
| Map pins | `map:…` | 2m |
| Reviews | `reviews:parking:{id}` | 10m |
| Vendor / member dashboards | `dashboard:vendor|member:{id}` | 5m |
| Owner listings | `owner-parkings:{id}` | 1m |
| Pending approval badge | `dashboard:pending-count:{id}` | 1m |
| User profile | `user:{id}` | 10m |
| Active parking passes | `user-passes:{id}` | 5m |
| Company quota allocations | `company-quota:{id}` | 5m |
| Company dashboard | `company-dashboard:{id}` | 2m |
| Availability forecasts | `parking-forecast:…` / `owner-parking-forecast:…` | 30s–1m |

Key helpers live in `ParkingApp.Application/Caching/CacheKeys.cs`.

### Accurate invalidation

All mutations go through `CacheInvalidation` (`ForParkingMutationAsync`, `ForBookingChangeAsync`, `ForReviewChangeAsync`, `ForUserPassesAsync`, etc.) so users do not see stale availability, search, map, dashboards, or pass pricing.

Covered write paths include:
- Parking create/update/delete/toggle (marketplace + corporate)
- Bookings: create, approve, reject, cancel, check-in/out, extensions, **reschedule**
- Payments: process / verify / extension confirm
- Reviews and owner responses
- Media upload/delete
- Parking pass create / corporate assign
- Corporate bookings, waitlist promotion, allocations, fixed slots
- Domain-event handlers (booking + parking) as a second line of defense after SaveChanges/outbox

Intentionally **not** cached: individual booking documents, chat, notifications, payment records (high churn / must stay transactional).

---

## Parking availability prediction

- Hybrid service: deterministic booking/capacity math + ML.NET regression on historical occupancy
- Respects active bookings, cancellations, listing status, spot counts
- ML adds weekday/hour trends, momentum, pricing, ratings (no paid ML cloud)
- Cached per parking / owner with short TTL; busted on booking and parking mutations
- APIs:
  - `GET /api/parking-availability/{parkingSpaceId}/forecast`
  - `GET /api/parking-availability/my-listings`

---

## Getting started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- PostgreSQL (or Supabase) with PostGIS for full geo features
- Optional: [Upstash Redis](https://upstash.com/) for distributed cache
- Git

### 1. Clone

```bash
git clone <repository-url>
cd ParkEase   # or ParkingApp/ParkEase depending on your layout
```

### 2. Backend

```bash
cd backend
dotnet restore

# Configure secrets (required for real DB / Redis / payments)
cd src/ParkingApp.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
dotnet user-secrets set "ConnectionStrings:Redis" "rediss://default:...@....upstash.io:6379"
dotnet user-secrets set "Jwt:SecretKey" "<at-least-32-chars>"
# Stripe, Resend, Storage:R2:*, Firebase:* as needed

cd ../..
dotnet run --project src/ParkingApp.API
```

API typically listens on the port in `Properties/launchSettings.json` (e.g. `http://localhost:5129`).

Apply EF migrations when the schema changes:

```bash
dotnet ef database update --project src/ParkingApp.Infrastructure --startup-project src/ParkingApp.API
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

Vite default: `http://localhost:5173` (listed in CORS).

### 4. Mobile (optional)

```bash
cd Mobile
npm install
npx expo start
```

Configure API base URL in `Mobile/src/config/environment.js`.

### 5. Tests

```bash
cd backend
dotnet test tests/ParkingApp.UnitTests
```

---

## Configuration reference

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL (+ PostGIS) |
| `ConnectionStrings:Redis` | Upstash / Redis (`rediss://` or host:port) |
| `Redis:*` | Instance prefix, TTLs, compression, timeouts, versioned namespaces |
| `Jwt:*` | Issuer, audience, access/refresh lifetimes, secret |
| `Stripe:*` | Payment secret / publishable keys |
| `Resend:*` | Transactional email |
| `Storage:Provider` / `Storage:R2:*` | Object storage (R2) |
| `Firebase:*` | FCM service account fields |
| `Corporate:WaitlistAutoPromotion` | Background waitlist poller |
| `Cors:AllowedOrigins` | SPA / deployed frontends |

**Local:** User Secrets (UserSecretsId on API project).  
**Production:** environment variables or secure appsettings; prefer env vars for passwords.

---

## Default test accounts

| Role | How |
|------|-----|
| Member / host | Register via UI (`POST /api/auth/register`) |
| Corporate admin | Create company after login, invite members |

There is no hardcoded seed admin in the default README flow.

---

## API endpoints (highlights)

### Authentication & users
- `POST /api/auth/register` — signup  
- `POST /api/auth/login` — JWT  
- `GET /api/users/me` — profile (cached)  
- `PUT /api/users/me` — update profile  

### Parking & search
- `GET /api/parking/search` — advanced search (cached)  
- `GET /api/parking/map` — map pins (cached)  
- `GET /api/parking/{id}` — detail + reservations (cached)  
- `POST /api/parking` — create listing  
- `POST /api/parking/{id}/toggle-active` — enable/disable  
- `GET /api/parking-availability/{parkingSpaceId}/forecast`  
- `GET /api/parking-availability/my-listings`  

### Bookings
- `POST /api/bookings` — create  
- `GET /api/bookings/my-bookings`  
- `GET /api/bookings/vendor-bookings`  
- `GET /api/bookings/pending-count` — vendor badge (cached)  
- `POST /api/bookings/{id}/extend`  
- `POST /api/bookings/{id}/approve-extension`  
- `POST /api/bookings/{id}/check-in` / `check-out`  

### Passes, payments, reviews
- `GET /api/passes/my` — active parking passes (cached)  
- `POST /api/passes` — create personal pass  
- `POST /api/passes/corporate` — assign corporate passes  
- Payments under the payments controller (process / verify)  
- `POST /api/reviews` — submit review  

### Corporate
- Base route: **`/api/v1/corporate`**  
- Companies, members, invitations, allocations, employee/visitor bookings, waitlist, invoices, company dashboard  

### Communication
- `GET /api/chat/conversations`  
- `POST /api/chat/send`  
- `GET /api/notifications`  

---

## Real-time

| Hub | Path (typical) |
|-----|----------------|
| Notifications | `/hubs/notifications` |
| Chat | `/hubs/chat` |

Push: Firebase Cloud Messaging when `Firebase:*` is configured.

| Domain signal | Meaning |
|---------------|---------|
| Booking requested / approved / cancelled | Status transitions + cache bust |
| Extension requested / approved | Host review + optional payment |
| Payment completed | Confirm booking/extension + cache bust |
| Check-in / check-out | Occupancy change + cache bust |
| Chat message | SignalR delivery |

---

## Domain events & outbox

Critical domain events (e.g. `BookingConfirmed`, `BookingCancelled`, `ParkingSpaceUpdated`) are persisted via the **transactional outbox** and processed by a background service. Cache invalidation handlers also run for parking and booking events so multi-instance deployments stay consistent even if a command-path invalidation is missed.

---

## License

This project is for educational / portfolio purposes unless otherwise stated.
