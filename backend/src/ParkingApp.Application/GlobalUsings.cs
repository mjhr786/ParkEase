// Shared CQRS abstractions only ΓÇö module-specific namespaces must be imported explicitly
// so cross-module (and module-local) dependencies stay visible at the file level.
global using ParkingApp.Application.CQRS;
