# ParkEase API — Mobile Developer Reference

**Purpose:** Match web app functionality on mobile using the same backend APIs.  
**Audience:** Mobile developers  
**Source of truth:** ASP.NET Core controllers under `backend/src/ParkingApp.API/Controllers`  
**Last updated:** 2026-07-12

---

## 1. Base configuration

| Item | Value |
|------|--------|
| API prefix | `/api` |
| Content type | `application/json` (except file upload & CSV export) |
| Auth scheme | JWT Bearer |
| Local base URL (dev) | `http://localhost:5129/api` |
| Health check | `GET /health` (no `/api` prefix) |
| Uploads (static) | `{host}/uploads/...` |
| SignalR hubs | `{host}/hubs/notifications`, `{host}/hubs/chat` |

### Authentication header

```
Authorization: Bearer <accessToken>
```

User id is taken from the JWT claim `NameIdentifier` — **never send `userId` in the body**.

### Token refresh (web pattern)

1. On `401` (except login/register), call `POST /api/auth/refresh` with `{ "refreshToken": "..." }`.
2. Store new `accessToken` + `refreshToken`.
3. Retry the original request.
4. If refresh fails → force logout / navigate to login.

### Standard response envelope

Most endpoints return:

```json
{
  "success": true,
  "message": "optional string or null",
  "data": { },
  "errors": ["optional validation messages"]
}
```

| Field | Type | Notes |
|-------|------|--------|
| `success` | bool | Operation result |
| `message` | string? | Human-readable status / error |
| `data` | T? | Payload |
| `errors` | string[]? | Validation errors |

**Exceptions (not always wrapped):**

- `GET /api/payments/stripe-config` → `{ "publishableKey": "..." }`
- **Vehicles** endpoints often return the raw DTO / array (or `204 No Content` on delete), not always `ApiResponse`
- **File upload** responses use `{ success, message, data }` shape (slightly custom)
- **CSV exports** return `text/csv` file download
- Some notification mark/delete endpoints return empty `200 OK` body

JSON property names are **camelCase**.

### Roles

| Role | Meaning |
|------|---------|
| `User` | Normal authenticated user (also hosts/vendors listing spaces) |
| `Admin` | Platform admin |

Corporate company roles (`Employee` / `Admin`) are **per company**, not global JWT roles. Company context is via URL path `{companyId}`.

---

## 2. Enums (send as numbers or names — numbers preferred)

### UserRole
| Value | Name |
|------:|------|
| 0 | Admin |
| 1 | User |

### BookingStatus
| Value | Name | Meaning |
|------:|------|---------|
| 0 | Pending | Waiting for owner approval |
| 1 | Confirmed | Approved + paid |
| 2 | InProgress | Checked in |
| 3 | Completed | Checked out |
| 4 | Cancelled | Cancelled |
| 5 | Expired | Expired |
| 6 | AwaitingPayment | Approved; waiting payment |
| 7 | Rejected | Rejected by owner |
| 8 | PendingExtension | Extension awaiting owner |
| 9 | AwaitingExtensionPayment | Extension approved; waiting payment |

### PaymentStatus
| 0 Pending | 1 Completed | 2 Failed | 3 Refunded | 4 PartialRefund |

### PricingType
| 0 Hourly | 1 Daily | 2 Weekly | 3 Monthly |

### ParkingType
| 0 Open | 1 Covered | 2 Garage | 3 Street | 4 Underground |

### VehicleType
| 0 Car | 1 Motorcycle | 2 SUV | 3 Truck | 4 Van | 5 Electric |

### PaymentMethod
| 0 CreditCard | 1 DebitCard | 2 UPI | 3 NetBanking | 4 Wallet |

### CompanyRole
| 0 Employee | 1 Admin |

### BillingType
| 0 ReservedSlots | 1 UsageBased |

### AllocationStatus
| 0 PendingApproval | 1 Active | 2 Rejected | 3 Expired |

### ParkingAllocationSource
| 0 VendorLease | 1 CompanyOwned |

### InvitationStatus
| 0 Pending | 1 Accepted | 2 Expired | 3 Cancelled |

### CorporateSlotType
| 0 Fixed | 1 Shared |

### PassTypeKind
| 0 Monthly | 1 Weekly | 2 Corporate |

### PassUsageMode
| 0 UnlimitedEntries | 1 LimitedHoursPerDay |

### NotificationType
| 0 BookingRequest | 1 BookingConfirmed | 2 BookingRejected | 3 PaymentReceived | 4 NewMessage | 5 SystemAlert |

---

## 3. Auth & users

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | Public | Sign up |
| POST | `/api/auth/login` | Public | Login → JWT |
| POST | `/api/auth/refresh` | Public | Refresh tokens |
| POST | `/api/auth/logout` | JWT | Invalidate session |
| POST | `/api/auth/change-password` | JWT | Change password |
| GET | `/api/users/me` | JWT | Current profile |
| PUT | `/api/users/me` | JWT | Update profile |
| DELETE | `/api/users/me` | JWT | Delete account |

### `POST /api/auth/register`

```json
{
  "email": "user@example.com",
  "password": "min8chars",
  "firstName": "Jane",
  "lastName": "Doe",
  "phoneNumber": "+91..."
}
```

**201** → `ApiResponse<TokenDto>`

### `POST /api/auth/login`

```json
{
  "email": "user@example.com",
  "password": "..."
}
```

**200** → tokens; **401** → invalid credentials

### `TokenDto` (`data`)

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "2026-07-12T12:00:00Z",
  "user": {
    "id": "guid",
    "email": "...",
    "firstName": "...",
    "lastName": "...",
    "phoneNumber": "...",
    "role": 1,
    "isEmailVerified": false,
    "isPhoneVerified": false,
    "createdAt": "..."
  }
}
```

### `POST /api/auth/refresh`

```json
{ "refreshToken": "..." }
```

### `POST /api/auth/change-password`

```json
{
  "currentPassword": "...",
  "newPassword": "min8chars"
}
```

### `PUT /api/users/me`

```json
{
  "firstName": "Jane",
  "lastName": "Doe",
  "phoneNumber": "+91..."
}
```

All fields optional.

---

## 4. Parking (marketplace / vendor listings)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/parking/{id}` | Public | Parking details |
| GET | `/api/parking/search` | Public | Search / list |
| GET | `/api/parking/map` | Public | Map pins |
| GET | `/api/parking/my-listings` | JWT (User,Admin) | Owner listings |
| POST | `/api/parking` | JWT (User,Admin) | Create listing |
| PUT | `/api/parking/{id}` | JWT (User,Admin) | Update listing |
| DELETE | `/api/parking/{id}` | JWT (User,Admin) | Delete listing |
| POST | `/api/parking/{id}/toggle-active` | JWT (User,Admin) | Enable/disable |

### Search / map query params (`ParkingSearchDto`)

| Param | Type | Notes |
|-------|------|--------|
| `state` | string | |
| `city` | string | |
| `address` | string | |
| `latitude` | double | For geo search |
| `longitude` | double | |
| `radiusKm` | double | |
| `startDateTime` | ISO datetime | Availability window |
| `endDateTime` | ISO datetime | |
| `minPrice` | decimal | |
| `maxPrice` | decimal | |
| `pricingType` | PricingType | |
| `parkingType` | ParkingType | |
| `vehicleType` | VehicleType | |
| `amenities` | string[] | |
| `minRating` | double | |
| `sortBy` | string | `price`, `rating`, `distance` |
| `sortDescending` | bool | default false |
| `page` | int | default 1 |
| `pageSize` | int | default 20 |

### `POST /api/parking` body (`CreateParkingSpaceDto`)

```json
{
  "title": "Covered lot near metro",
  "description": "...",
  "address": "123 Main St",
  "city": "Mumbai",
  "state": "Maharashtra",
  "country": "India",
  "postalCode": "400001",
  "latitude": 19.07,
  "longitude": 72.87,
  "parkingType": 1,
  "totalSpots": 20,
  "hourlyRate": 50,
  "dailyRate": 300,
  "weeklyRate": 1500,
  "monthlyRate": 5000,
  "openTime": "08:00:00",
  "closeTime": "22:00:00",
  "is24Hours": false,
  "amenities": ["CCTV", "EV Charging"],
  "allowedVehicleTypes": [0, 2],
  "imageUrls": [],
  "specialInstructions": "...",
  "zoneCode": null
}
```

`PUT` uses the same fields, all optional (`UpdateParkingSpaceDto`), plus optional `isActive`.

---

## 5. Parking availability (forecast)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/parking-availability/{parkingSpaceId}/forecast` | Public | Forecast buckets |
| GET | `/api/parking-availability/my-listings` | JWT (User,Admin) | Forecast for owner’s listings |

### Query params

| Param | Default (by endpoint) | Description |
|-------|------------------------|-------------|
| `horizonHours` | 24 (single) / 12 (my-listings) | Forecast horizon |
| `intervalMinutes` | 60 | Bucket size |

---

## 6. Bookings (consumer + vendor)

Base route: `/api/bookings`  
Class-level: **JWT required** (except calculate-price).

| Method | Path | Auth notes | Description |
|--------|------|------------|-------------|
| GET | `/api/bookings/{id}` | JWT | Booking by id |
| GET | `/api/bookings/reference/{reference}` | JWT | By reference code |
| GET | `/api/bookings/my-bookings` | JWT | Member booking history |
| GET | `/api/bookings/vendor-bookings` | JWT (User,Admin) | Host’s booking inbox |
| GET | `/api/bookings/pending-count` | JWT (User,Admin) | Pending request badge |
| GET | `/api/bookings/parking-space/{parkingSpaceId}` | JWT (User,Admin) | Bookings for one space |
| POST | `/api/bookings/calculate-price` | **Public** | Price quote |
| POST | `/api/bookings` | JWT | Create booking |
| PUT | `/api/bookings/{id}` | JWT | Update booking |
| POST | `/api/bookings/{id}/cancel` | JWT | Cancel |
| POST | `/api/bookings/{id}/approve` | JWT (User,Admin) | Vendor approve |
| POST | `/api/bookings/{id}/reject` | JWT (User,Admin) | Vendor reject |
| POST | `/api/bookings/{id}/check-in` | JWT | Check-in |
| POST | `/api/bookings/{id}/check-out` | JWT | Check-out |
| POST | `/api/bookings/{id}/extend` | JWT | Request extension |
| POST | `/api/bookings/{id}/approve-extension` | JWT (User,Admin) | Approve extension |
| POST | `/api/bookings/{id}/reject-extension` | JWT (User,Admin) | Reject extension |

> **Note:** Web uses `/api/bookings` (not `/api/v2/bookings`). Older README text may say v2 — ignore it.

### List filters (`BookingFilterDto` query)

| Param | Type |
|-------|------|
| `userId` | guid? |
| `parkingSpaceId` | guid? |
| `status` | BookingStatus? |
| `startDate` | datetime? |
| `endDate` | datetime? |
| `page` | int (default 1) |
| `pageSize` | int (default 20) |

### `POST /api/bookings/calculate-price`

```json
{
  "parkingSpaceId": "guid",
  "startDateTime": "2026-07-12T10:00:00Z",
  "endDateTime": "2026-07-12T14:00:00Z",
  "pricingType": 0,
  "discountCode": null
}
```

### `POST /api/bookings`

```json
{
  "parkingSpaceId": "guid",
  "startDateTime": "2026-07-12T10:00:00Z",
  "endDateTime": "2026-07-12T14:00:00Z",
  "pricingType": 0,
  "vehicleType": 0,
  "slotNumber": 3,
  "vehicleNumber": "MH12AB1234",
  "vehicleModel": "Swift",
  "vehicleColor": "White",
  "discountCode": null
}
```

### `PUT /api/bookings/{id}`

```json
{
  "startDateTime": "...",
  "endDateTime": "...",
  "vehicleType": 0,
  "vehicleNumber": "...",
  "vehicleModel": "..."
}
```

### Cancel / reject / reject-extension

```json
{ "reason": "Plans changed" }
```

Reject/reject-extension: `reason` optional.

### Extend

```json
{ "newEndDateTime": "2026-07-12T18:00:00Z" }
```

### Typical booking flow (match web)

1. Search / open parking detail  
2. `calculate-price`  
3. `POST /bookings`  
4. Vendor: `approve` or `reject`  
5. Member: payment (`/payments/create-order` + `verify`)  
6. Member: `check-in` → later `check-out`  
7. Optional: `extend` → vendor `approve-extension` / `reject-extension` → pay if needed  
8. Optional: `cancel` with reason  

---

## 7. Payments

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/payments/stripe-config` | Public | Publishable key |
| GET | `/api/payments/{id}` | JWT | Payment by id |
| GET | `/api/payments/booking/{bookingId}` | JWT | Payment for booking |
| POST | `/api/payments` | JWT | Process payment |
| POST | `/api/payments/create-order` | JWT | Create gateway order (Razorpay) |
| POST | `/api/payments/verify` | JWT | Verify gateway payment |
| POST | `/api/payments/refund` | JWT | Refund |

### Create order

Body is a **raw GUID JSON string** (not an object):

```json
"3fa85f64-5717-4562-b3fc-2c963f66afa6"
```

Web: `JSON.stringify(bookingId)`.

### Verify payment

```json
{
  "bookingId": "guid",
  "razorpayPaymentId": "...",
  "razorpayOrderId": "...",
  "razorpaySignature": "..."
}
```

### Process payment

```json
{
  "bookingId": "guid",
  "paymentMethod": 2
}
```

### Refund

```json
{
  "paymentId": "guid",
  "amount": 100.00,
  "reason": "..."
}
```

---

## 8. Reviews

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/reviews/{id}` | Public | Review by id |
| GET | `/api/reviews/parking-space/{parkingSpaceId}` | Public | Reviews for space |
| POST | `/api/reviews` | JWT | Create review |
| PUT | `/api/reviews/{id}` | JWT | Update own review |
| DELETE | `/api/reviews/{id}` | JWT | Delete own review |
| POST | `/api/reviews/{id}/owner-response` | JWT (User,Admin) | Owner reply |

### Create

```json
{
  "parkingSpaceId": "guid",
  "bookingId": "guid-or-null",
  "rating": 5,
  "title": "Great spot",
  "comment": "..."
}
```

`rating`: 1–5.

### Owner response

```json
{ "response": "Thanks for parking with us!" }
```

---

## 9. Favorites

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/favorites` | JWT | Saved spaces |
| POST | `/api/favorites/{parkingSpaceId}/toggle` | JWT | Save / unsave |

Toggle response `data` is `bool` (favorited state after toggle).

---

## 10. Vehicles

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/vehicles` | JWT | My vehicles |
| POST | `/api/vehicles` | JWT | Add vehicle |
| PUT | `/api/vehicles/{id}` | JWT | Update |
| DELETE | `/api/vehicles/{id}` | JWT | Delete → **204** |

### Create / update body

```json
{
  "licensePlate": "MH12AB1234",
  "make": "Maruti",
  "model": "Swift",
  "color": "White",
  "type": 0,
  "isDefault": true
}
```

**Mobile note:** These endpoints return raw vehicle objects/arrays (not always `ApiResponse`). Handle both if the client is generic.

---

## 11. Parking passes

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/passes` | JWT | Buy / create pass |
| GET | `/api/passes/my` | JWT | Active passes |
| POST | `/api/passes/corporate` | JWT **Admin** | Assign corporate batch |

### Create pass

```json
{
  "passType": 0,
  "startDateUtc": "2026-07-01T00:00:00Z",
  "endDateUtc": "2026-07-31T23:59:59Z",
  "parkingSpaceId": "guid-or-null",
  "parkingZoneCode": null,
  "usageMode": 0,
  "dailyHourLimit": null,
  "discountPercentage": 10
}
```

---

## 12. Chat

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/chat/conversations` | JWT | Thread list |
| GET | `/api/chat/conversations/{conversationId}/messages` | JWT | Messages (newest first) |
| POST | `/api/chat/send` | JWT | Send message (+ SignalR push) |
| POST | `/api/chat/conversations/{conversationId}/read` | JWT | Mark read |
| GET | `/api/chat/unread-count` | JWT | Total unread |

### Query

- Conversations: `page` (default 1), `pageSize` (default 20)  
- Messages: `page` (default 1), `pageSize` (default 50)

### Send message

```json
{
  "parkingSpaceId": "guid",
  "content": "Is EV charging free?",
  "conversationId": null
}
```

Pass `conversationId` when continuing an existing thread; omit/null to create.

### Real-time (SignalR)

- Hub: `/hubs/chat` (JWT required)  
- Server → client events:
  - `ReceiveMessage` — `ChatMessageDto`
  - `MessagesRead` — `conversationId`
  - `Error` — string  
- Client → server: `SendMessage(parkingSpaceId, content, conversationId?)`

Connect with access token (query or header per SignalR client config).

---

## 13. Notifications (in-app history)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/notifications` | JWT | Paginated list + unread count |
| PUT | `/api/notifications/{id}/read` | JWT | Mark one read |
| PUT | `/api/notifications/read-all` | JWT | Mark all read |
| DELETE | `/api/notifications/{id}` | JWT | Delete one |
| DELETE | `/api/notifications/clear-all` | JWT | Clear all |

Query: `page` (default 1), `pageSize` (default 20).

### Real-time

- Hub: `/hubs/notifications` (JWT)  
- User is auto-joined to group `user_{userId}`  
- Client can call `AcknowledgeNotification(notificationId)`

### Device tokens (push / FCM)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/device-tokens/register` | JWT | Upsert FCM token |

```json
{
  "deviceId": "stable-device-id",
  "platform": "android",
  "fcmToken": "...",
  "appVersion": "1.0.0"
}
```

Call after login, on app launch, and on FCM token refresh. Same `deviceId` updates the token (no duplicates).

---

## 14. Files (parking media)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/files/parking/{parkingSpaceId}/upload` | JWT (User,Admin) | Multipart upload |
| DELETE | `/api/files/parking/{parkingSpaceId}/{fileName}` | JWT (User,Admin) | Delete file |
| GET | `/api/files/parking/{parkingSpaceId}` | Public | List file URLs |
| POST | `/api/files/parking/{parkingSpaceId}/sign-upload` | JWT (User,Admin) | Presigned URL |
| POST | `/api/files/parking/{parkingSpaceId}/confirm-upload` | JWT (User,Admin) | Confirm S3-style upload |

### Multipart upload

- Form field name: **`files`** (multiple)  
- Do **not** set `Content-Type: application/json` — use multipart  
- Allowed: JPG, PNG, WEBP (max 5MB), MP4, WEBM (max 50MB)

### Presigned flow

1. `sign-upload` with `{ "fileName", "contentType" }`  
2. PUT file to returned `uploadUrl`  
3. `confirm-upload` with `{ "fileUrls": ["publicUrl", ...] }`

---

## 15. Dashboards (marketplace)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/dashboard/vendor` | JWT (User,Admin) | Host stats |
| GET | `/api/dashboard/member` | JWT | Member stats |

---

## 16. Corporate module

Base route: **`/api/v1/corporate`**  
All endpoints: **JWT required**.

Web stores active company in `activeCompanyId` and puts `companyId` **in the URL path** (not only headers). Optional header `X-Company-Id` is used by the frontend for some legacy cases — prefer path params.

### 16.1 Companies

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/corporate/companies` | Create company |
| GET | `/api/v1/corporate/me/companies` | Companies I belong to |
| GET | `/api/v1/corporate/companies/{companyId}` | Company details |
| PUT | `/api/v1/corporate/companies/{companyId}` | Update company |
| GET | `/api/v1/corporate/companies/{companyId}/dashboard` | Admin dashboard metrics |
| GET | `/api/v1/corporate/companies/{companyId}/dashboard/export` | CSV export (`text/csv`) |

**Create company**

```json
{
  "name": "Acme Corp",
  "registrationNumber": "CIN123",
  "contactEmail": "admin@acme.com",
  "contactPhone": "+91...",
  "billingAddress": "...",
  "billingType": 0
}
```

### 16.2 Members & invitations

| Method | Path | Description |
|--------|------|-------------|
| GET | `.../companies/{companyId}/members?page&pageSize` | List members |
| POST | `.../companies/{companyId}/members` | Add existing user by email |
| PUT | `.../companies/{companyId}/members/{membershipId}` | Update role/priority/code |
| DELETE | `.../companies/{companyId}/members/{membershipId}` | Remove member |
| GET | `.../companies/{companyId}/invitations` | List invitations |
| POST | `.../companies/{companyId}/invitations` | Invite by email |
| DELETE | `.../companies/{companyId}/invitations/{invitationId}` | Cancel invite |
| POST | `.../companies/{companyId}/invitations/{invitationId}/resend` | Resend invite |
| POST | `/api/v1/corporate/invitations/accept` | Accept invite (body = token string) |

**Add member**

```json
{
  "email": "emp@acme.com",
  "role": 0,
  "employeeCode": "E001",
  "priority": 1
}
```

**Invite**

```json
{ "email": "new@acme.com", "role": 0 }
```

**Accept invitation** — body is a **JSON string** (token):

```json
"invitation-token-value"
```

**Update member**

```json
{
  "role": 1,
  "priority": 2,
  "employeeCode": "E002",
  "clearEmployeeCode": false
}
```

### 16.3 Allocations & company parking

| Method | Path | Description |
|--------|------|-------------|
| GET | `.../companies/{companyId}/allocations` | Company allocations |
| POST | `.../companies/{companyId}/allocations` | Request lease from vendor |
| GET | `/api/v1/corporate/vendor/allocations` | Vendor’s incoming allocation requests |
| POST | `/api/v1/corporate/allocations/{allocationId}/approve` | Vendor approve |
| POST | `/api/v1/corporate/allocations/{allocationId}/reject` | Vendor reject (body = reason string) |
| PUT | `.../companies/{companyId}/allocations/{allocationId}/policy` | Booking policy |
| PUT | `.../companies/{companyId}/allocations/{allocationId}/contract` | Contract terms |
| POST | `.../companies/{companyId}/allocations/{allocationId}/fixed-slots` | Assign fixed slot |
| DELETE | `.../companies/{companyId}/allocations/{allocationId}/fixed-slots/{membershipId}` | Remove fixed slot |
| GET | `.../companies/{companyId}/parking-spaces` | Company-owned spaces |
| POST | `.../companies/{companyId}/parking-spaces` | Create company parking (same body as marketplace create) |
| PUT | `.../companies/{companyId}/parking-spaces/{parkingSpaceId}` | Update |
| DELETE | `.../companies/{companyId}/parking-spaces/{parkingSpaceId}` | Retire |
| POST | `.../companies/{companyId}/parking-spaces/{parkingSpaceId}/toggle-active` | Toggle active |
| POST | `.../companies/{companyId}/parking-spaces/{parkingSpaceId}/allocations` | Create owned allocation |

**Request vendor allocation**

```json
{
  "parkingSpaceId": "guid",
  "totalSlots": 10,
  "fixedSlots": 4,
  "sharedSlots": 6,
  "monthlyRate": 50000,
  "startDate": "2026-07-01",
  "endDate": "2027-06-30",
  "leaseReference": "L-001",
  "policy": {
    "maxBookingsPerEmployeePerDay": 1,
    "maxBookingsPerEmployeePerWeek": 5,
    "priorityThreshold": 1,
    "allowedStartTime": "08:00:00",
    "allowedEndTime": "20:00:00",
    "allowWeekends": false
  }
}
```

**Reject allocation** body = JSON string:

```json
"Not enough capacity"
```

**Assign fixed slot**

```json
{ "membershipId": "guid", "slotNumber": 5 }
```

**Update contract**

```json
{
  "monthlyRate": 55000,
  "startDate": "2026-07-01",
  "endDate": "2027-06-30",
  "leaseReference": "L-001"
}
```

### 16.4 Corporate bookings & waitlist

| Method | Path | Description |
|--------|------|-------------|
| GET | `.../companies/{companyId}/bookings` | List (filters below) |
| GET | `.../companies/{companyId}/bookings/export` | CSV export |
| POST | `.../companies/{companyId}/bookings/employee` | Employee booking |
| POST | `.../companies/{companyId}/bookings/visitor` | Visitor booking |
| POST | `.../companies/{companyId}/bookings/{bookingId}/cancel` | Cancel corporate booking |
| GET | `.../companies/{companyId}/waitlist` | Waitlist |
| DELETE | `.../companies/{companyId}/waitlist/{waitlistEntryId}` | Cancel waitlist entry |
| POST | `.../companies/{companyId}/waitlist/{waitlistEntryId}/promote` | Promote waitlist → booking |

**List query params:** `page`, `pageSize`, `status` (BookingStatus), `isVisitor` (bool), `fromUtc`, `toUtc`

**Employee booking**

```json
{
  "allocationId": "guid",
  "startDateTime": "2026-07-12T09:00:00Z",
  "endDateTime": "2026-07-12T18:00:00Z",
  "vehicleType": 0,
  "vehicleNumber": "MH12AB1234"
}
```

**Visitor booking**

```json
{
  "allocationId": "guid",
  "startDateTime": "...",
  "endDateTime": "...",
  "visitorName": "Guest Name",
  "visitorLicensePlate": "MH01XX9999",
  "accessExpiry": "2026-07-12T20:00:00Z"
}
```

**Cancel**

```json
{ "reason": "Meeting cancelled" }
```

Reservation result may include either a booking, a waitlist entry, and/or a fraud assessment (`CorporateReservationResultDto`).

---

## 17. Admin outbox (platform Admin only)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/admin/outbox` | List messages (`status`, `type`, `page`, `pageSize`) |
| GET | `/api/admin/outbox/{id}` | Message detail |
| POST | `/api/admin/outbox/{id}/requeue` | Requeue one |
| POST | `/api/admin/outbox/requeue-failed` | Requeue all failed |
| POST | `/api/admin/outbox/process?batchSize=50` | Process batch now |

Usually **not** needed in mobile consumer apps.

---

## 18. Health

| Method | Path | Auth |
|--------|------|------|
| GET | `/health` | Public |

```json
{ "status": "healthy", "timestamp": "..." }
```

---

## 19. Feature map: web ↔ mobile parity checklist

Use this to implement screens in the same order as the web app.

### Consumer (member)

| Web capability | Endpoints |
|----------------|-----------|
| Register / login / logout / refresh | Auth |
| Profile + change password | Users + Auth |
| Search parking + map | `parking/search`, `parking/map` |
| Parking detail + forecast | `parking/{id}`, `parking-availability/.../forecast` |
| Favorites | Favorites |
| Vehicles | Vehicles |
| Price calc + book | Bookings |
| Pay | Payments |
| My bookings, cancel, check-in/out, extend | Bookings |
| Reviews | Reviews |
| Chat | Chat + `/hubs/chat` |
| In-app notifications | Notifications + `/hubs/notifications` |
| Push notifications | Device tokens |
| Member dashboard | `dashboard/member` |
| Parking passes | Passes |

### Vendor / host

| Web capability | Endpoints |
|----------------|-----------|
| My listings CRUD + toggle | Parking |
| Image upload | Files |
| Vendor bookings approve/reject | Bookings |
| Extension approve/reject | Bookings |
| Pending badge | `bookings/pending-count` |
| Vendor dashboard | `dashboard/vendor` |
| Owner review reply | Reviews owner-response |
| Availability forecasts for listings | `parking-availability/my-listings` |
| Corporate lease requests | Corporate vendor allocations |

### Corporate

| Web capability | Endpoints |
|----------------|-----------|
| Company create / switch / update | Corporate companies |
| Dashboard + CSV | Dashboard + export |
| Members + invites | Members / invitations |
| Owned parking + allocations | Parking-spaces + allocations |
| Lease request + policy / fixed slots | Allocations |
| Employee / visitor book | Bookings employee/visitor |
| Waitlist manage | Waitlist |
| Corporate booking list + cancel + export | Bookings |

---

## 20. Error handling tips

| HTTP | Typical meaning |
|-----:|-----------------|
| 400 | Validation / business rule (`success: false`, `errors` or `message`) |
| 401 | Missing/invalid JWT or bad login |
| 403 | Not owner / not authorized for resource |
| 404 | Not found |
| 500 | Server error |

Always check both HTTP status and `success` flag when present.

---

## 21. Quick path index (all routes)

### Public / mixed

```
GET    /health
POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/refresh
GET    /api/parking/{id}
GET    /api/parking/search
GET    /api/parking/map
GET    /api/parking-availability/{id}/forecast
POST   /api/bookings/calculate-price
GET    /api/payments/stripe-config
GET    /api/reviews/{id}
GET    /api/reviews/parking-space/{parkingSpaceId}
GET    /api/files/parking/{parkingSpaceId}
```

### Auth required (selected)

```
POST   /api/auth/logout
POST   /api/auth/change-password
GET|PUT|DELETE /api/users/me

GET|POST /api/parking ...
GET|POST|PUT|DELETE /api/bookings ...
GET|POST /api/payments ...
GET|POST /api/favorites ...
GET|POST|PUT|DELETE /api/vehicles ...
GET|POST /api/passes ...
GET|POST /api/chat ...
GET|PUT|DELETE /api/notifications ...
POST   /api/device-tokens/register
GET    /api/dashboard/vendor|member
*      /api/v1/corporate/**
*      /api/files/parking/** (write)
*      /api/admin/outbox/** (Admin)
```

### SignalR

```
/hubs/notifications
/hubs/chat
```

---

## 22. Related docs

| Doc | Purpose |
|-----|---------|
| `backend/mobile_dev_handoff.md` | FCM device token registration deep-dive |
| `docs/corporate-parking-flow.md` | Corporate product flow |
| Frontend `src/services/api.js` | Web client usage (source of parity) |
| Frontend `src/services/corporateService.js` | Corporate web client |

If an endpoint is missing from this document but exists in controllers, treat the controller as authoritative and update this file.
)
