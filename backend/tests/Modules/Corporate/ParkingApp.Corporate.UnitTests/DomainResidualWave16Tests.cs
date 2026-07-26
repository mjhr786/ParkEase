using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>Wave 16 domain polish: AccessPolicy windows, Quota edges, Company create guards.</summary>
public class DomainResidualWave16Tests
{
    [Fact]
    public void AccessPolicy_IsActiveAndExpired()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var policy = AccessPolicy.Create("ka01ab", start, end);

        policy.AllowedVehiclePlate.Should().Be("KA01AB");
        policy.IsActive(start.AddMinutes(30)).Should().BeTrue();
        policy.IsActive(start.AddHours(-1)).Should().BeFalse();
        policy.IsExpired(end.AddMinutes(1)).Should().BeTrue();
        policy.IsExpired(end).Should().BeFalse();
        policy.IsVehicleAllowed("KA01AB").Should().BeTrue();
        policy.IsVehicleAllowed("ka01ab").Should().BeTrue();
        policy.IsVehicleAllowed("other").Should().BeFalse();
        policy.QrCodeToken.Should().StartWith("VIS-");
    }

    [Fact]
    public void AccessPolicy_EmptyExplicitToken_Throws()
    {
        var start = DateTime.UtcNow;
        var act = () => AccessPolicy.Create("KA01", start, start.AddHours(1), "  ");
        act.Should().Throw<ArgumentException>().WithMessage("*QR*");
    }

    [Fact]
    public void AccessPolicy_LocalKind_Normalizes()
    {
        var startLocal = DateTime.SpecifyKind(new DateTime(2026, 7, 22, 12, 0, 0), DateTimeKind.Local);
        var endLocal = startLocal.AddHours(2);
        var policy = AccessPolicy.Create("KA01XX", startLocal, endLocal, "QR-LOCAL");
        policy.AccessStartUtc.Kind.Should().Be(DateTimeKind.Utc);
        policy.AccessExpiryUtc.Kind.Should().Be(DateTimeKind.Utc);
        policy.IsActive(policy.AccessStartUtc.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void Quota_HasSharedAndUnallocated()
    {
        var full = Quota.Create(5, 2, 3);
        full.HasFixedSlots.Should().BeTrue();
        full.HasSharedSlots.Should().BeTrue();
        full.UnallocatedSlots.Should().Be(0);

        var partial = Quota.Create(10, 1, 2);
        partial.UnallocatedSlots.Should().Be(7);
    }

    [Fact]
    public void Company_Create_ValidatesRequiredFields()
    {
        var emptyName = () => Company.Create("  ", "REG", "a@b.com", "1", "addr", BillingType.UsageBased, Guid.NewGuid());
        emptyName.Should().Throw<ArgumentException>().WithMessage("*name*");

        var emptyReg = () => Company.Create("Acme", "  ", "a@b.com", "1", "addr", BillingType.UsageBased, Guid.NewGuid());
        emptyReg.Should().Throw<ArgumentException>().WithMessage("*Registration*");

        var emptyEmail = () => Company.Create("Acme", "REG", "  ", "1", "addr", BillingType.UsageBased, Guid.NewGuid());
        emptyEmail.Should().Throw<ArgumentException>().WithMessage("*email*");

        var emptyCreator = () => Company.Create("Acme", "REG", "a@b.com", "1", "addr", BillingType.UsageBased, Guid.Empty);
        emptyCreator.Should().Throw<ArgumentException>().WithMessage("*Created*");
    }

    [Fact]
    public void Company_Create_NormalizesFieldsAndAddsAdmin()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create(
            "  Acme  ", " reg-x ", "  Admin@Acme.COM  ", " 555 ", "  Addr ",
            BillingType.ReservedSlots, adminId);

        company.Name.Should().Be("Acme");
        company.RegistrationNumber.Should().Be("REG-X");
        company.ContactEmail.Should().Be("admin@acme.com");
        company.ContactPhone.Should().Be("555");
        company.BillingAddress.Should().Be("Addr");
        company.Memberships.Should().ContainSingle(m => m.UserId == adminId && m.IsAdmin);
        company.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Company_CalculateBookingAmount_InvalidInputs_Throw()
    {
        var company = Company.Create("Acme", "REG-C", "a@acme.com", "1", "a", BillingType.UsageBased, Guid.NewGuid());
        var negRate = () => company.CalculateBookingAmount(-1m, TimeSpan.FromHours(1));
        negRate.Should().Throw<ArgumentOutOfRangeException>();
        var zeroDur = () => company.CalculateBookingAmount(10m, TimeSpan.Zero);
        zeroDur.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Company_GetWaitlistPosition_Missing_Throws()
    {
        var company = Company.Create("Acme", "REG-W", "a@acme.com", "1", "a", BillingType.UsageBased, Guid.NewGuid());
        var act = () => company.GetWaitlistPosition(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Waitlist*");
    }

    [Fact]
    public void Invitation_MarkExpired_OnlyWhenPending()
    {
        var inv = EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        inv.Cancel();
        inv.MarkExpired(); // no-op when cancelled
        inv.Status.Should().Be(InvitationStatus.Cancelled);
    }

    [Fact]
    public void CorporateBookingDraft_AwaitingPayment_ShouldConfirm()
    {
        var start = DateTime.UtcNow.AddHours(1);
        var draft = new CorporateBookingDraft(
            Guid.NewGuid(), Guid.NewGuid(), start, start.AddHours(2),
            ParkingApp.Marketplace.Contracts.Enums.BookingStatus.AwaitingPayment,
            VehicleType.Car, null);

        var adj = draft.ToConfirmationAdjustment(1);
        adj.ShouldConfirm.Should().BeTrue();
        draft.VehicleNumber.Should().BeNull();
    }
}
