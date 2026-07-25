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

## Guidance

- Prefer **module** test projects for pure Domain/Application tests of that BC.
- Keep architecture ProjectReference rules in `ParkingApp.UnitTests/Architecture`.
- Module projects should reference that module’s Domain/Application/Contracts/Infrastructure (+ Contracts of collaborators only when needed).
- Avoid referencing host `ParkingApp.API` from module tests.
