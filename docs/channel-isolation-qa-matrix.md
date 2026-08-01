# Channel Isolation — QA Matrix (Staging)

Use when **`ChannelIsolation:Enabled=true`** (Staging / local flag-on).  
Mark each row **Pass / Fail / N/A** and attach request id or screenshot.

**Personas**

| ID | Setup |
| --- | --- |
| M | Marketplace JWT (`channel=Marketplace`) |
| C-Admin | Corporate JWT bound, `company_role=Admin` |
| C-Member | Corporate JWT bound, non-Admin member |
| C-Boot | Corporate JWT bootstrap (no `company_id`) |
| Vendor | Marketplace JWT, parking owner with pending allocation |
| Anon | No Authorization header |

Denial contract: HTTP **403**, body `code` / errors include **`channel_forbidden`**.

---

## A. Flag + channel-context

| # | Case | Steps | Expected | Result |
| --- | --- | --- | --- | --- |
| A1 | Isolation on | `GET /api/auth/channel-context` with any valid JWT | `data.isolationEnabled === true` | |
| A2 | Company cache ignore | SPA with stale `activeCompanyId` while Marketplace | Shell stays marketplace (JWT channel); no corporate chrome | |
| A3 | API flag off rollback | Set Enabled=false; re-fetch channel-context | `isolationEnabled === false`; matrix denials stop; **SPA shells still channel-only (PR10b)** | |
| A4 | No Personal Mode | Marketplace authenticated header | Corporate workspace CTA only — no Personal Mode toggle | |

---

## B. Auth / bind / refresh

| # | Case | Steps | Expected | Result |
| --- | --- | --- | --- | --- |
| B1 | Marketplace login | `POST /api/auth/login` | Tokens; channel Marketplace (or default) | |
| B2 | Corporate login bound | `POST /api/auth/login/corporate` + `companyId` (member) | Corporate + company_id + company_role | |
| B3 | Corporate multi-company | Login without companyId when multiple memberships | 400 `company_selection_required` (no half-mint) | |
| B4 | Bootstrap | Corporate login with zero memberships | Bootstrap Corporate; no company_id; `isBootstrap` | |
| B5 | Switch M→C | `POST /api/auth/channel` `{ channel: Corporate, companyId }` | New tokens bound | |
| B6 | Switch C→M | `{ channel: Marketplace }` | Company claims cleared | |
| B7 | Bootstrap → company | After create company, tokens re-minted **or** `POST /auth/channel` with new companyId | Bound Admin; not bootstrap | |
| B8 | Refresh preserves channel | Corporate session → `POST /api/auth/refresh` | Still Corporate + same company_id | |
| B9 | Invite accept | Accept invite on marketplace or corporate | Membership; may need channel switch to use corp APIs | |

---

## C. Cross-channel denials (core isolation)

| # | Case | Actor | Request | Expected | Result |
| --- | --- | --- | --- | --- | --- |
| C1 | Corp dashboard from marketplace | M | `GET /api/v1/corporate/companies/{id}/dashboard` (or home) | 403 channel_forbidden | |
| C2 | Marketplace booking from corporate | C-Admin | `POST /api/bookings` | 403 channel_forbidden | |
| C3 | Favorites from corporate | C-Member | `GET /api/favorites` | 403 channel_forbidden | |
| C4 | My listings from corporate | C-Admin | `GET /api/parking/my-listings` | 403 channel_forbidden | |
| C5 | Create company from marketplace | M | `POST /api/v1/corporate/companies` | 403 channel_forbidden | |
| C6 | Company API as bootstrap | C-Boot | `GET …/companies/{id}/…` | 403 channel_forbidden | |
| C7 | Company id mismatch | C-Admin company A | Hit company B routes / wrong `X-Company-Id` | 403 channel_forbidden | |
| C8 | Payments from corporate | C-Member | `GET /api/payments/…` | 403 channel_forbidden | |

---

## D. Lease-browse (KD-17) + corporate book

| # | Case | Actor | Steps | Expected | Result |
| --- | --- | --- | --- | --- | --- |
| D1 | Search as CA | C-Admin | `GET /api/parking/search` | 200; SPA Lease Browse works | |
| D2 | Search as member | C-Member | `GET /api/parking/search` | 403 (CA only) | |
| D3 | Get by id as CA | C-Admin | `GET /api/parking/{id}` | 200 (public/marketplace listing) | |
| D4 | Request allocation | C-Admin | From Lease Browse → request allocation | Creates PendingApproval; **no** marketplace book UI | |
| D5 | ParkingDetails marketplace only | M | Open listing details | Book space only; no corporate allocation dual UI | |
| D6 | Employee book | C-Member | Company Allocations → Book space on active allocation | Corporate book succeeds | |
| D7 | Visitor book | C-Admin/Member | Same modal visitor path | Visitor book / policy enforced | |

---

## E. Vendor allowlist (KD-6)

| # | Case | Actor | Request | Expected | Result |
| --- | --- | --- | --- | --- | --- |
| E1 | List vendor allocations | Vendor (M) | `GET /api/v1/corporate/vendor/allocations` | 200 | |
| E2 | Approve allocation | Vendor (M) | `POST …/allocations/{id}/approve` | 200 / domain OK | |
| E3 | Vendor path as corporate | C-Admin | Same vendor list | 403 channel_forbidden | |

---

## F. Web shells (SPA)

| # | Case | Steps | Expected | Result |
| --- | --- | --- | --- | --- |
| F1 | Corporate shell gate | Visit `/corporate/*` on Marketplace session | Redirect / Corporate login path | |
| F2 | Nav Lease Browse | Corporate Admin nav | `/corporate/lease-browse` | |
| F3 | Member Lease Browse | Corporate non-Admin | Denied message (Admin only) | |
| F4 | 403 interceptor | Force channel_forbidden | Toast / redirect without infinite loop | |
| F5 | Create company flow | Bootstrap → create → re-mint | Lands bound Admin dashboard | |
| F6 | Company switcher | Multi-company user | Switch re-mints tokens; data matches company | |

---

## G. Inventory isolation (PR4 / PR4b — flag-independent)

| # | Case | Steps | Expected | Result |
| --- | --- | --- | --- | --- |
| G1 | Corporate-only listing | Marketplace search | Not in public search | |
| G2 | Staged consumer booking | Consumer booking list | Corporate-staged rows hidden | |

---

## Smoke automation

```powershell
# From ParkEase repo root (ParkEase/)
.\scripts\smoke-channel-isolation.ps1 `
  -BaseUrl "https://YOUR-STAGING-HOST" `
  -Email "user@example.com" `
  -Password "..." `
  -CompanyId "optional-guid-if-multi-company"
```

Exit code `0` = automated subset green. Complete manual rows A–G before sign-off.

---

## Sign-off

| Role | Name | Date | Notes |
| --- | --- | --- | --- |
| QA | | | |
| Backend | | | |
| Web | | | |

**Staging smoke green** → ready for PR10a planning (prod flag). Mobile is a separate track and does not block staging web soak.
