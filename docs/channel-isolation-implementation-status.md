# Channel Isolation — Implementation Status (Web + Backend)

| Field | Value |
| --- | --- |
| **Branch** | `channel-isolation-wip` |
| **Option** | A — hard product channels in modular monolith |
| **Scope delivered** | Backend API + Web SPA |
| **Out of scope here** | Mobile (PR11 M1/M2) — separate track |
| **Date** | 2026-08-01 |
| **Commit policy** | Staged on branch; author commits manually |

Canonical plan: repo root `Implementation Plan Channel Isolation.md`.

---

## PR completion (this branch)

| PR | Title | Status |
| --- | --- | --- |
| **PR1** | JWT channel claims + session bind + token mint | Done |
| **PR2** | `ChannelAuthorizationMiddleware` + allowlist matrix | Done |
| **PR3** | Corporate login / bootstrap / switch channel | Done |
| **PR4** | Corporate-only inventory isolation (data plane) | Done |
| **PR4b** | Corporate-staged booking privacy in marketplace lists | Done |
| **PR5** | Refresh re-bind + channel-context (`isolationEnabled`) | Done |
| **PR6** | Web corporate login + auth channel state | Done |
| **PR7** | Web shells / `CorporateChannelRoute` / founder create-company | Done |
| **PR8** | Lease-browse + marketplace `ParkingDetails` split | Done |
| **PR9** | Staging flag ON + QA matrix + runbook + smoke | Done |
| **PR10a** | Production flag ON + cutover checklist | Done |
| **PR10b** | Delete soft-mode SPA chrome | Done |
| **PR11 M1** | Mobile channel auth | **Deferred** (not this branch) |
| **PR11 M2** | Mobile shell isolation | **Deferred** (not this branch) |

---

## Flag matrix

| Environment | `ChannelIsolation:Enabled` |
| --- | --- |
| Base (`appsettings.json`) | `false` |
| Development | `false` |
| Staging | **`true`** |
| Production | **`true`** |

Override without redeploy: `ChannelIsolation__Enabled=true|false`.

---

## Ops docs

| Doc | Purpose |
| --- | --- |
| `docs/channel-isolation-runbook.md` | Config, local staging run, rollback, soak |
| `docs/channel-isolation-prod-cutover.md` | Prod enable checklist, smoke, support copy |
| `docs/channel-isolation-qa-matrix.md` | Manual pass/fail matrix |
| `docs/API_ENDPOINTS_CHANNEL_ISOLATION.md` | Auth surfaces + `channel_forbidden` contract |
| `docs/corporate-parking-flow.md` | Product flow (lease-browse / Allocations book) |
| `scripts/smoke-channel-isolation.ps1` | Automated smoke against a base URL |

---

## Rollback (post-PR10b)

| Layer | Action |
| --- | --- |
| **API** | `ChannelIsolation__Enabled=false` — matrix denials stop; no migration reverse |
| **SPA UX** | Redeploy **prior frontend artifact** (pre-PR10b). Flag-off does **not** restore Personal Mode / soft company chrome |

---

## Residual semantics (not soft-mode)

These remain by design:

- `isCorporateMode === (channel === 'Corporate')` — alias for corporate pages; **not** driven by bare `localStorage`.
- `activeCompanyId` cache — mirrors JWT `company_id` for `corporateService` path helpers only.
- `isolationEnabled` from `GET /api/auth/channel-context` — diagnostic / future UI; does not restore dual chrome.

---

## Suggested manual commit split (optional)

Author may commit as one WIP commit or split approximately:

1. Backend: claims, middleware, inventory, bookings, migrations, tests  
2. Frontend: auth/shells/lease-browse/soft-mode removal + tests  
3. Config + docs + smoke script  

No commits were made by the implementation agent.
