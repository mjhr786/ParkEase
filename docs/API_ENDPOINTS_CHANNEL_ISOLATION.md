# API endpoints — channel isolation notes

Companion to controller inventory in the implementation plan. Focus: **auth channel bind** and **denial contract** used by web SPA when isolation is on.

## Denial contract

When `ChannelIsolation:Enabled=true` and the session channel is not allowed for the route:

| Field | Value |
| --- | --- |
| HTTP status | `403 Forbidden` |
| `ApiResponse.Code` | `channel_forbidden` |
| `ApiResponse.Errors` | includes `channel_forbidden` |
| `ApiResponse.Message` | `Access denied for the current product channel.` |
| Log | Warning `ChannelIsolation denied: {Reason} method=… path=… channel=… rule=…` |

When flag is **off**, middleware does not emit this code (handlers may still return other 403s).

Default when authenticated path has **no** matrix rule: **deny** (KD-21).

---

## Auth / channel bind

Base: `/api/auth`

| Method | Path | Auth | Channels | Notes |
| --- | --- | --- | --- | --- |
| POST | `/login` | Anon | — | Marketplace session mint (default product entry) |
| POST | `/register` | Anon | — | Same |
| POST | `/login/corporate` | Anon | — | Body: `{ email, password, companyId? }`. Zero memberships → bootstrap Corporate. One membership → auto-bind. Many + no companyId → `company_selection_required` |
| POST | `/channel` | JWT | All product | Body: `{ channel, companyId?, bootstrap? }`. Re-mint access+refresh; updates `User.SessionChannel*` |
| GET | `/channel-context` | JWT | All product | Returns `channel`, `companyId`, `companyRole`, `isBootstrap`, **`isolationEnabled`**, memberships |
| POST | `/refresh` | Refresh body | — | **Must preserve** bound Corporate channel + company (no silent demotion) |
| POST | `/logout` | JWT | All | |
| POST | `/change-password` | JWT | All | |

### Token / context fields (isolation-relevant)

| Field | Meaning |
| --- | --- |
| JWT `channel` | `Marketplace` \| `Corporate` \| `Admin` |
| JWT `company_id` | Present when Corporate bound |
| JWT `company_role` | e.g. `Admin`, `Member` |
| `isBootstrap` | Corporate without company_id |
| `isolationEnabled` | Host `ChannelIsolation:Enabled` at mint/context time |

### Headers

| Header | Use |
| --- | --- |
| `Authorization: Bearer …` | Access token |
| `X-Company-Id` | Optional; when isolation on + Corporate bound, must match JWT `company_id` if present |

---

## High-traffic matrix reminders

| Area | Marketplace | Corporate bound | Corporate bootstrap | Notes |
| --- | --- | --- | --- | --- |
| `GET /api/parking/search`, `/map`, `/{id}` | ✓ | **CA only** (lease-browse) | ✗ | Non-admin corporate → 403 |
| `POST /api/bookings/**` | ✓ | ✗ | ✗ | Corporate book via corporate APIs only |
| `GET/POST /api/favorites/**` | ✓ | ✗ | ✗ | |
| `GET /api/parking/my-listings`, owner mutations | ✓ | ✗ | ✗ | |
| `POST /api/v1/corporate/companies` | ✗ | ✓ | ✓ (B) | Create company |
| `* /api/v1/corporate/companies/{companyId}/**` | ✗ | ✓ + company match | ✗ | Dashboard, allocations, members, bookings |
| `GET /api/v1/corporate/me/companies` | ✓ (CTA) | ✓ | ✓ | |
| Vendor alloc list/approve/reject | ✓ | ✗ | ✗ | KD-6 allowlist |
| `POST …/invitations/accept` | ✓ | ✓ | ✓ | All product channels |
| Chat / notifications / hubs | ✓ | ✓ | ✗ | Bound only |
| `/api/admin/**` | Platform Admin role | same | same | KD-13 |

Full template table: implementation plan § Route / API Allowlist Matrix.  
Authoritative code: `ChannelRouteMatrix.Rules` in `ChannelRouteRule.cs`.

---

## Corporate booking vs marketplace book

| Action | API surface | Channel |
| --- | --- | --- |
| Marketplace consumer book | `POST /api/bookings` | Marketplace |
| Request B2B allocation | `POST /api/v1/corporate/companies/{id}/allocations` | Corporate + Admin |
| Employee / visitor corporate book | Corporate company booking endpoints | Corporate bound |
| SPA lease discovery | `GET /api/parking/search` + get-by-id | Corporate + **Admin** only |
| SPA marketplace details | `/parking/:id` → `createBooking` only | Marketplace (no dual allocation UI) |

---

## Frontend route map (web)

| SPA path | Channel expectation |
| --- | --- |
| `/login`, `/search`, `/parking/:id`, vendor, bookings | Marketplace |
| `/corporate/login` | Corporate entry |
| `/corporate/*` | `CorporateChannelRoute` — bound Corporate JWT |
| `/corporate/lease-browse` | Company Admin; uses parking search/get + allocation request |

---

## Related

- Runbook: `docs/channel-isolation-runbook.md`
- QA matrix: `docs/channel-isolation-qa-matrix.md`
- Smoke: `scripts/smoke-channel-isolation.ps1`
