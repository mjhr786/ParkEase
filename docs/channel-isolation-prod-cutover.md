# Channel Isolation — Production Cutover (PR10a)

**Scope:** Backend + web only. Mobile channel auth is a **separate track** and is not required for this cutover checklist when product accepts marketplace-mobile + corporate-web split until mobile lands.

## What this PR does

| Change | Detail |
| --- | --- |
| Flag | `ChannelIsolation:Enabled=true` in **`appsettings.Production.json`** |
| Soft-mode | **Removed in PR10b** — shells follow JWT channel only |
| Middleware | Enforces route matrix when flag on |
| SPA | Already reads `isolationEnabled` from tokens / `channel-context` |

## Pre-deploy gates

- [ ] PR1–PR8 code deployed (or same release train as this config).
- [ ] Staging soak with flag on (PR9) completed; critical matrix rows green (`docs/channel-isolation-qa-matrix.md`).
- [ ] Migrations applied on prod DB:
  - User session channel bind (`SessionChannel`, `SessionCompanyId`, `SessionCompanyRole`)
  - Booking `IsCorporateStaged` (if not already)
- [ ] Web frontend with corporate login, shells, lease-browse, marketplace-only `ParkingDetails` is the live SPA.
- [ ] Support briefed (section below).
- [ ] Rollback owner named (who can flip env var / redeploy config).

## Deploy steps

1. Deploy API build that includes channel middleware + production appsettings (or set env override after deploy).
2. Confirm process environment is **Production** so `appsettings.Production.json` loads.
3. Optional host override (same effect without file edit):

   ```text
   ChannelIsolation__Enabled=true
   ChannelIsolation__TreatMissingClaimAs=Marketplace
   ChannelIsolation__EnforceCompanyClaimMatch=true
   ChannelIsolation__VendorAllocationAllowlistEnabled=true
   ```

4. Deploy matching web SPA if not already co-hosted.
5. Run post-deploy smoke (below).
6. Watch logs 30–60 minutes for unexpected `ChannelIsolation denied` spikes.

## Post-deploy smoke (web + API)

Prefer automated:

```powershell
.\scripts\smoke-channel-isolation.ps1 `
  -BaseUrl "https://parkeaseapp.runasp.net" `
  -Email "<prod-or-canary-user>" `
  -Password "<secret>" `
  -CompanyId "<guid-if-multi>"
```

Manual minimum:

| # | Check | Pass criteria |
| --- | --- | --- |
| 1 | `GET /api/auth/channel-context` after marketplace login | `isolationEnabled: true` |
| 2 | Marketplace search / book happy path | 200 / book OK |
| 3 | Corporate login → company dashboard | 200 |
| 4 | Marketplace JWT → company dashboard API | **403** `channel_forbidden` |
| 5 | Corporate JWT → `POST /api/bookings` | **403** `channel_forbidden` |
| 6 | Corporate Admin lease-browse search | 200 |
| 7 | Vendor (marketplace) allocation list | 200 or empty OK |
| 8 | Token refresh on corporate session | Stays Corporate + same company |
| 9 | SPA: `/corporate/*` without corporate channel | Redirect corporate login |
| 10 | SPA: marketplace details | Book only (no dual allocation UI) |

## Rollback drill (must work)

**Instant API rollback:**

```text
ChannelIsolation__Enabled=false
```

Or redeploy production config with `"Enabled": false`. No data model reverse required.

**Verify after API rollback:**

- `channel-context` → `isolationEnabled: false`
- Cross-channel calls are no longer matrix-denied (membership checks still apply)
- **SPA shells remain JWT channel-only** (PR10b) — soft dual chrome is **not** restored by the flag alone

**SPA UX rollback:** Redeploy a **prior frontend artifact** (pre-PR10b) if you need Personal Mode / soft company chrome again.

## Support communications (copy/paste)

### Internal #eng / #support

> **Channel isolation is ON in production.**  
> Users must use **Corporate login / channel switch** for company dashboards, allocations, and corporate booking. Marketplace JWT can no longer call company-scoped corporate APIs (HTTP 403, code `channel_forbidden`).  
> **Lease requests:** company Admins use **Lease Browse** under Corporate, not the public listing dual UI.  
> **Vendor allocation approve/reject** still uses the **marketplace (vendor) session**.  
> **Rollback:** set `ChannelIsolation__Enabled=false` (no migration reverse) for API matrix. SPA soft dual chrome was removed in PR10b — UX rollback needs a prior frontend artifact.

### User-facing (if status page / email)

> We’ve separated **personal parking** and **company parking** sessions for security.  
> To manage your company, open **Corporate** sign-in (or switch to your company from the account menu).  
> Searching for spaces to lease for your company is under **Lease Browse** in the company area.  
> If something looks wrong after signing in, sign out and use the matching Corporate or personal login.

### Known limitations to tell support

| Topic | Guidance |
| --- | --- |
| Two tabs (marketplace + corporate) | Last login/switch wins; other tab’s refresh may fail — re-login |
| Mobile app | May not yet bind Corporate JWT; corporate web is the supported path for company features until mobile ships |
| 403 `channel_forbidden` | Wrong product session — switch channel or use Corporate login, not “broken permissions” |
| Missing company in token | Complete company selection or create company (bootstrap → re-mint) |

## Observability

| Signal | Alert / action |
| --- | --- |
| Spike `ChannelIsolation denied` | Check client bugs vs attacks; path+channel in log |
| Corporate refresh demotion reports | Treat as Sev-1 for isolation; consider flag off |
| Elevated 401 on refresh | Single-session last-mint conflicts (document, not always a bug) |

## Soft-mode cleanup (PR10b — done)

Personal Mode / `activeCompanyId`-driven dual chrome is removed from the SPA.

- **API rollback** still works via `ChannelIsolation__Enabled=false`.
- **UX rollback** after PR10b = **prior frontend artifact only**.

## Related

- Runbook: `docs/channel-isolation-runbook.md`
- QA matrix: `docs/channel-isolation-qa-matrix.md`
- API notes: `docs/API_ENDPOINTS_CHANNEL_ISOLATION.md`
- Smoke: `scripts/smoke-channel-isolation.ps1`
