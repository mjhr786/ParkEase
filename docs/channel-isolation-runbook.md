# Channel Isolation — Runbook

**PR9** enabled isolation on **Staging**. **PR10a** enables it on **Production**. **PR10b** removed soft-mode SPA chrome (Personal Mode / `activeCompanyId`-driven shells).

## Configuration

| Environment | File | `ChannelIsolation:Enabled` |
| --- | --- | --- |
| Default / base | `appsettings.json` | `false` |
| Development | `appsettings.Development.json` | `false` (override via env for local soak) |
| **Staging** | `appsettings.Staging.json` | **`true`** |
| **Production** | `appsettings.Production.json` | **`true`** (PR10a) |

**Production cutover checklist:** `docs/channel-isolation-prod-cutover.md`

### Full section (staging defaults)

```json
"ChannelIsolation": {
  "Enabled": true,
  "TreatMissingClaimAs": "Marketplace",
  "EnforceCompanyClaimMatch": true,
  "VendorAllocationAllowlistEnabled": true
}
```

| Key | Meaning |
| --- | --- |
| `Enabled` | When `false`, middleware is a no-op (membership/ownership still apply). |
| `TreatMissingClaimAs` | Missing JWT `channel` claim → treat as Marketplace during soak. Switch to `Reject` after full cutover if desired. |
| `EnforceCompanyClaimMatch` | Corporate tokens: JWT `company_id` must match route `{companyId}` / `X-Company-Id` when present. |
| `VendorAllocationAllowlistEnabled` | Vendor B2B allocation approve/reject/list only on Marketplace channel. |

### Host env overrides (no redeploy of appsettings)

```text
ChannelIsolation__Enabled=true|false
ChannelIsolation__TreatMissingClaimAs=Marketplace|Reject
ChannelIsolation__EnforceCompanyClaimMatch=true|false
ChannelIsolation__VendorAllocationAllowlistEnabled=true|false
ASPNETCORE_ENVIRONMENT=Staging
```

### Local staging-like run

```powershell
cd ParkEase/backend/src/ParkingApp.API
$env:ASPNETCORE_ENVIRONMENT = "Staging"
# secrets still from user-secrets / env
dotnet run --launch-profile Staging
```

Or keep Development and only flip the flag:

```powershell
$env:ChannelIsolation__Enabled = "true"
dotnet run
```

---

## Rollback

### API (instant)

Set **`ChannelIsolation:Enabled=false`** on the staging host (config portal, env var, or redeploy with flag off). Middleware becomes a no-op (membership/ownership still apply). Matrix denials stop.

### SPA UX (PR10b shipped)

- Shell chrome is **JWT channel only** — no Personal Mode soft toggle.
- **API rollback** does **not** restore soft dual chrome.
- **UX rollback** = redeploy a prior frontend artifact (pre-PR10b).
- Clients still read `isolationEnabled` from `GET /api/auth/channel-context` for diagnostics.

---

## Staging enable checklist (PR9)

1. Deploy backend with PR1–PR8 + PR9 (`appsettings.Staging.json` present).
2. Confirm host `ASPNETCORE_ENVIRONMENT=Staging` (or env override `ChannelIsolation__Enabled=true`).
3. Confirm migrations applied (`User.SessionChannel*`, `Booking.IsCorporateStaged` if not already).
4. Run unit suite (middleware + corporate auth) in CI.
5. Run `scripts/smoke-channel-isolation.ps1` against staging base URL.
6. Walk **manual matrix** in `docs/channel-isolation-qa-matrix.md`.
7. Watch logs for `ChannelIsolation denied` (expected on intentional denials; spike may mean client bugs).
8. Soak ≥ 1 business day before treating staging as green for prod.

## Production enable checklist (PR10a)

Use the full checklist in **`docs/channel-isolation-prod-cutover.md`**. Summary:

1. Staging matrix green; migrations on prod DB.
2. Deploy API with `appsettings.Production.json` → `ChannelIsolation:Enabled: true`.
3. Deploy SPA that includes PR6–PR8 shells (**PR10b** soft-mode removal is already on this branch).
4. Post-deploy smoke (script + manual web rows).
5. Brief support; confirm rollback owner knows `ChannelIsolation__Enabled=false` **and** prior SPA artifact for UX rollback.

---

## Observability

| Signal | Where | Action |
| --- | --- | --- |
| `ChannelIsolation denied: {Reason} method=… path=… channel=…` | API Warning | Security / client wrong-channel |
| `channel.switch` / bootstrap mint logs | Identity | Product health |
| Refresh demotes Corporate → Marketplace | Client reports / session bugs | Block cutover; fix refresh bind |
| `GET /api/auth/channel-context` → `isolationEnabled: true` | Smoke | Staging flag live |

---

## Known limitations (staging soak)

- Single refresh token per user (last mint wins); marketplace + corporate tabs conflict after switch.
- Mobile channel auth is **out of scope for this PR set** until a dedicated mobile track; do not gate staging web soak on mobile.
- Missing `channel` claim treated as Marketplace (`TreatMissingClaimAs`).

---

## Related docs

- `docs/channel-isolation-qa-matrix.md` — pass/fail matrix
- `docs/API_ENDPOINTS_CHANNEL_ISOLATION.md` — auth + denial contract
- `scripts/smoke-channel-isolation.ps1` — automated smoke
- Implementation plan: repo root `Implementation Plan Channel Isolation.md`
