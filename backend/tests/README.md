# Backend tests

## Layout

| Project | Purpose |
| --- | --- |
| `ParkingApp.UnitTests` | Host / cross-cutting / architecture / legacy integration-style unit tests |
| `Modules/Identity/ParkingApp.Identity.UnitTests` | Identity module isolation tests |
| `Modules/Marketplace/ParkingApp.Marketplace.UnitTests` | Marketplace module isolation tests |
| `Modules/Corporate/ParkingApp.Corporate.UnitTests` | Corporate module tests (pilot migration from monolithic suite) |
| `Modules/Messaging/ParkingApp.Messaging.UnitTests` | Messaging module isolation tests |
| `Modules/Notifications/ParkingApp.Notifications.UnitTests` | Notifications module isolation tests |

Shared packages: `tests/Directory.Build.props` (xUnit, Moq, AwesomeAssertions, Test SDK, coverlet).

## Run

```bash
# All tests in solution
dotnet test ParkingApp.sln

# One module (fast feedback)
dotnet test tests/Modules/Corporate/ParkingApp.Corporate.UnitTests/ParkingApp.Corporate.UnitTests.csproj

# Architecture / host suite only
dotnet test tests/ParkingApp.UnitTests/ParkingApp.UnitTests.csproj --filter "FullyQualifiedName~Architecture"
```

## CI

GitHub Actions workflow: **`.github/workflows/unit-tests.yml`**

| Job | Command | Fail on |
| --- | --- | --- |
| Backend | `dotnet test ParkingApp.sln` + Coverlet collect | Test failures |
| Backend (Corp floors) | Re-run Corporate module with Coverlet + `tests/assert-corporate-coverage.ps1` | Corp Domain or App line rate below **90%** |
| Frontend | `npm run test:coverage` (Vitest) | Test failures **or** FE utils/services coverage floors |

Coverage artifacts (TRX + Cobertura + FE coverage HTML) upload on every run. **Hard Domain/Application 100% thresholds are deferred** (see `docs/Unit_Test_Coverage_Plan.md` Phase 7.1). **Selective Corporate Domain + Application ≥90% line floors are enforced** (Wave 17).

## Coverage (Coverlet)

```powershell
# From backend/ (preferred helper)
powershell -File ./tests/run-coverage.ps1
# or: pwsh ./tests/run-coverage.ps1
```

```bash
# Manual (uses tests/coverlet.runsettings — excludes Migrations / Program / Designers)
dotnet test ParkingApp.sln \
  --collect:"XPlat Code Coverage" \
  --settings tests/coverlet.runsettings \
  --results-directory ./TestResults

# Optional HTML report (install once: dotnet tool install -g dotnet-reportgenerator-globaltool)
reportgenerator \
  -reports:TestResults/**/coverage.cobertura.xml \
  -targetdir:TestResults/CoverageReport \
  -reporttypes:Html;TextSummary
```

Excludes live in `tests/coverlet.runsettings` (`**/Migrations/**`, `**/*Designer.cs`, `**/Program.cs`, `ExcludeFromCodeCoverageAttribute`).

Corp Domain EF private parameterless constructors are marked `[ExcludeFromCodeCoverage]` (Wave 24). Full exclude policy: **`docs/Unit_Test_Coverage_Plan.md` §15**.

Track progress and layer targets in **`docs/Unit_Test_Coverage_Plan.md`**.

**Mobile unit tests are out of scope** for the current coverage initiative.

### Corporate floors (local)

```powershell
dotnet test tests/Modules/Corporate/ParkingApp.Corporate.UnitTests/ParkingApp.Corporate.UnitTests.csproj `
  --collect:"XPlat Code Coverage" `
  --settings tests/coverlet.runsettings `
  --results-directory ./TestResults-corp

powershell -File ./tests/assert-corporate-coverage.ps1 -ResultsDirectory ./TestResults-corp
```

## Guidance

- Prefer **module** test projects for pure Domain/Application tests of that BC.
- Keep architecture ProjectReference rules in `ParkingApp.UnitTests/Architecture`.
- Module projects should reference that module’s Domain/Application/Contracts/Infrastructure (+ Contracts of collaborators only when needed).
- Avoid referencing host `ParkingApp.API` from module tests.
