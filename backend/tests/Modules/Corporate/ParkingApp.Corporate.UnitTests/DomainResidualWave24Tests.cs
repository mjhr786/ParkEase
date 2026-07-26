using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// Wave 24 domain residual: EF private ctors excluded via [ExcludeFromCodeCoverage];
/// remaining Coverlet edges (invitation token miss, Unspecified DateTimeKind paths).
/// </summary>
public class DomainResidualWave24Tests
{
    private static Company CreateCompany(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return Company.Create("Wave24 Co", "REG-W24", "w24@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
    }

    private static BookingPolicy DefaultPolicy() =>
        BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);

    [Fact]
    public void Company_AcceptInvitation_UnknownToken_Throws()
    {
        var company = CreateCompany(out _);

        var act = () => company.AcceptInvitation("not-a-real-token", Guid.NewGuid(), "user@hire.com");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid or expired invitation*");
    }

    [Fact]
    public void Company_CreateOwnedAllocation_UnspecifiedKind_NormalizesViaEnsureOverlap()
    {
        var company = CreateCompany(out var adminId);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);

        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            start,
            end,
            parkingCapacity: 10,
            DefaultPolicy());

        allocation.Should().NotBeNull();
        allocation.Status.Should().Be(AllocationStatus.Active);
    }

    [Fact]
    public void Company_UpdateMember_DemoteAdmin_WhenOtherAdminsExist_Succeeds()
    {
        var company = CreateCompany(out var adminId);
        var secondAdmin = company.AddMember(adminId, Guid.NewGuid(), CompanyRole.Admin);

        // Demote second admin while first remains — exercises last-admin guard false branch.
        var updated = company.UpdateMember(adminId, secondAdmin.Id, role: CompanyRole.Employee);

        updated.Role.Should().Be(CompanyRole.Employee);
    }

    [Fact]
    public void WaitlistEntry_CreateEmployee_UnspecifiedKind_NormalizesToUtc()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Unspecified);

        var entry = CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            start,
            end,
            VehicleType.Car,
            "ka01ab1234",
            priorityAtRequest: 3);

        entry.RequestedStartDateTime.Kind.Should().Be(DateTimeKind.Utc);
        entry.RequestedEndDateTime.Kind.Should().Be(DateTimeKind.Utc);
        entry.VehicleNumber.Should().Be("KA01AB1234");
    }

    [Fact]
    public void WaitlistEntry_CreateVisitor_UnspecifiedKind_NormalizesAccessWindow()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Unspecified);
        var expiry = new DateTime(2026, 7, 22, 18, 0, 0, DateTimeKind.Unspecified);

        var entry = CorporateWaitlistEntry.CreateVisitor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            start,
            end,
            "Guest",
            "KA09XY9999",
            expiry,
            priorityAtRequest: 1);

        entry.IsVisitorBooking.Should().BeTrue();
        entry.RequestedStartDateTime.Kind.Should().Be(DateTimeKind.Utc);
        entry.VisitorLicensePlate.Should().NotBeNullOrWhiteSpace();
    }
}
