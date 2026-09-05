using System;
using System.Collections.Generic;
using FluentAssertions;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using Xunit;

namespace ParkingApp.Corporate.UnitTests;

public class CorporateDtoCoverageTests
{
    [Fact]
    public void AllCorporateDtos_PropertyGettersAndConstructors_CanBeExercised()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // CompanyDto & CreateCompanyDto & UpdateCompanyDto
        var compDto = new CompanyDto(companyId, "Name", "REG", "a@b.com", "123", "Addr", BillingType.ReservedSlots, true, 5, 2, now, "slug");
        compDto.Id.Should().Be(companyId);
        compDto.Name.Should().Be("Name");
        compDto.RegistrationNumber.Should().Be("REG");
        compDto.ContactEmail.Should().Be("a@b.com");
        compDto.ContactPhone.Should().Be("123");
        compDto.BillingAddress.Should().Be("Addr");
        compDto.BillingType.Should().Be(BillingType.ReservedSlots);
        compDto.IsActive.Should().BeTrue();
        compDto.MemberCount.Should().Be(5);
        compDto.ActiveAllocationCount.Should().Be(2);
        compDto.CreatedAt.Should().Be(now);
        compDto.Slug.Should().Be("slug");

        var createComp = new CreateCompanyDto("Name", "REG", "a@b.com", "123", "Addr", BillingType.ReservedSlots);
        createComp.Name.Should().Be("Name");

        var createResult = new CreateCompanyResultDto(compDto, new { Token = "abc" });
        createResult.Company.Should().Be(compDto);
        createResult.Session.Should().NotBeNull();

        var updateComp = new UpdateCompanyDto("Name", "a@b.com", "123", "Addr", BillingType.UsageBased, "slug");
        updateComp.Slug.Should().Be("slug");

        // Membership DTOs
        var memDto = new MembershipDto(id, adminId, "User", "user@b.com", CompanyRole.Admin, "EMP1", 1, true, now, companyId);
        memDto.Id.Should().Be(id);
        memDto.UserId.Should().Be(adminId);
        memDto.UserName.Should().Be("User");
        memDto.UserEmail.Should().Be("user@b.com");
        memDto.Role.Should().Be(CompanyRole.Admin);
        memDto.EmployeeCode.Should().Be("EMP1");
        memDto.Priority.Should().Be(1);
        memDto.IsActive.Should().BeTrue();
        memDto.CreatedAt.Should().Be(now);
        memDto.CompanyId.Should().Be(companyId);

        var compMembers = new CompanyMembersDto(new List<MembershipDto> { memDto }, 1, 1, 10);
        compMembers.TotalCount.Should().Be(1);

        var addMem = new AddMemberDto("user@b.com", CompanyRole.Employee, "EMP1", 2);
        addMem.Email.Should().Be("user@b.com");

        var updateMem = new UpdateMemberDto(CompanyRole.Admin, 5, "EMP2", true);
        updateMem.ClearEmployeeCode.Should().BeTrue();

        // Invitation DTOs
        var inviteMem = new InviteMemberDto("user@b.com", CompanyRole.Employee);
        inviteMem.Email.Should().Be("user@b.com");

        var invDto = new InvitationDto(id, "user@b.com", CompanyRole.Employee, InvitationStatus.Pending, now.AddDays(7), now, "tok");
        invDto.InvitationToken.Should().Be("tok");
        invDto.Status.Should().Be(InvitationStatus.Pending);

        // Allocation DTOs
        var pool = new SlotPoolDto(10, 5, 5);
        pool.TotalSlots.Should().Be(10);
        pool.FixedSlots.Should().Be(5);
        pool.SharedSlots.Should().Be(5);

        var policy = new BookingPolicyDto(2, 10, 1, TimeSpan.FromHours(8), TimeSpan.FromHours(18), true);
        policy.AllowWeekends.Should().BeTrue();

        var fixedAssign = new FixedSlotAssignmentDto(id, "User", 1, now, VehicleClass.FourWheeler);
        fixedAssign.SlotNumber.Should().Be(1);

        var assignFixed = new AssignFixedSlotDto(id, 1, VehicleClass.FourWheeler);
        assignFixed.SlotNumber.Should().Be(1);

        var parkAlloc = new ParkingAllocationDto(
            id, companyId, Guid.NewGuid(), "Lot 1", 10, 5, 5, 100m, now, now.AddMonths(1), AllocationStatus.Active, ParkingAllocationSource.CompanyOwned, null, "REF", adminId, now, policy, new List<FixedSlotAssignmentDto> { fixedAssign }, now, "Vendor", pool, pool);
        parkAlloc.ParkingSpaceTitle.Should().Be("Lot 1");
        parkAlloc.TwoWheeler.Should().Be(pool);

        var updateAlloc = new UpdateAllocationContractDto(200m, now, now.AddMonths(2), "REF2");
        updateAlloc.MonthlyRate.Should().Be(200m);

        var allocateSlots = new AllocateParkingSlotsDto(Guid.NewGuid(), 10, 5, 5, 100m, now, now.AddMonths(1), "REF", policy, pool, pool);
        allocateSlots.TotalSlots.Should().Be(10);

        var createOwnedAlloc = new CreateOwnedParkingAllocationDto(Guid.NewGuid(), 10, 5, 5, 100m, now, now.AddMonths(1), policy, pool, pool);
        createOwnedAlloc.TotalSlots.Should().Be(10);

        // Corporate Parking Space DTOs
        var spaceDto = new CorporateParkingSpaceDto(
            id, companyId, "Title", "Desc", "Addr", "City", "State", "Country", "12345", 12.34, 56.78, ParkingType.Open, 50, 40, 10m, 50m, 200m, 500m, TimeSpan.FromHours(8), TimeSpan.FromHours(20), false, new List<string> { "EV" }, new List<VehicleType> { VehicleType.Car }, new List<string> { "http://img" }, true, true, "Instructions", "ZONE1", now, 10, 40);
        spaceDto.Title.Should().Be("Title");
        spaceDto.TwoWheelerPhysicalSpots.Should().Be(10);

        var updateSpaceDto = new UpdateCorporateParkingSpaceDto("New Title", "Desc", "Addr", "City", "State", "Country", "12345", 12.34, 56.78, ParkingType.Open, 50, 10m, 50m, 200m, 500m, TimeSpan.FromHours(8), TimeSpan.FromHours(20), false, new List<string> { "EV" }, new List<VehicleType> { VehicleType.Car }, new List<string> { "http://img" }, "Instructions", "ZONE1", 10, 40);
        updateSpaceDto.Title.Should().Be("New Title");

        // Corporate Booking DTOs
        var corpBooking = new CorporateBookingDto(
            id, Guid.NewGuid(), "REF123", CorporateSlotType.Shared, 5, false, null, null, now, now.AddHours(2), BookingStatus.Confirmed, "QR123", now, id, "Lot 1", id, "User", "u@b.com", 15m, "MH01");
        corpBooking.BookingReference.Should().Be("REF123");

        var filter = new CorporateBookingListFilter(BookingStatus.Confirmed, false, now, now.AddDays(1));
        filter.Status.Should().Be(BookingStatus.Confirmed);

        var waitlistDto = new CorporateWaitlistDto(id, id, false, now, now.AddHours(2), "MH01", null, null, WaitlistStatus.Pending, 1, 1, now);
        waitlistDto.VehicleNumber.Should().Be("MH01");

        var fraudAssess = new FraudAssessmentDto(CorporateFraudRiskLevel.Low, false, "Ok");
        fraudAssess.RiskLevel.Should().Be(CorporateFraudRiskLevel.Low);

        var reservResult = new CorporateReservationResultDto(corpBooking, waitlistDto, fraudAssess);
        reservResult.Booking.Should().Be(corpBooking);

        var bookCorp = new BookCorporateParkingDto(id, now, now.AddHours(2), VehicleType.Car, "MH01");
        bookCorp.VehicleNumber.Should().Be("MH01");

        var bookVisitor = new BookVisitorParkingDto(id, now, now.AddHours(2), "Visitor", "MH02", now.AddHours(4), VehicleType.Car);
        bookVisitor.VisitorName.Should().Be("Visitor");

        // Dashboard & Invoice DTOs
        var dashDto = new CompanyDashboardDto(
            10, 8, 2, 2, 1, 50, 1, 0, 100, 10, 250m, 5000m, 75.0, new List<DashboardChartDataDto>(), new List<AllocationUtilizationDto>(), 0, 0, new List<PeakHourDto>(), new List<FraudAlertDto>(), 1, new List<ExpiringAllocationDto>());
        dashDto.TotalMembers.Should().Be(10);

        var genInv = new GenerateCorporateInvoiceDto(DateOnly.FromDateTime(now), DateOnly.FromDateTime(now.AddMonths(1)));
        genInv.PeriodStart.Should().Be(DateOnly.FromDateTime(now));

        var markPaid = new MarkInvoicePaidDto("PAY123", "Notes");
        markPaid.PaymentReference.Should().Be("PAY123");

        var voidInv = new VoidInvoiceDto("Reason");
        voidInv.Reason.Should().Be("Reason");

        var invLine = new CorporateInvoiceLineDto(id, CorporateInvoiceLineType.ReservedCapacity, id, id, "Line", 1m, 100m, 100m);
        invLine.Description.Should().Be("Line");

        var invSummary = new CorporateInvoiceSummaryDto(id, "INV-001", BillingType.ReservedSlots, DateOnly.FromDateTime(now), DateOnly.FromDateTime(now.AddMonths(1)), CorporateInvoiceStatus.Paid, "INR", 100m, 18m, 118m, 1, now, now, now, "REF");
        invSummary.InvoiceNumber.Should().Be("INV-001");

        var invDetail = new CorporateInvoiceDetailDto(id, "INV-001", BillingType.ReservedSlots, DateOnly.FromDateTime(now), DateOnly.FromDateTime(now.AddMonths(1)), CorporateInvoiceStatus.Paid, "INR", 100m, 18m, 118m, adminId, now, now, adminId, now, adminId, "REF", "Notes", null, null, null, new List<CorporateInvoiceLineDto> { invLine });
        invDetail.InvoiceNumber.Should().Be("INV-001");

        var invList = new CorporateInvoiceListDto(new List<CorporateInvoiceSummaryDto> { invSummary }, 1, 1, 10);
        invList.TotalCount.Should().Be(1);

        var memberBookings = new MemberBookingsDto(new List<CorporateBookingDto> { corpBooking }, 1, 1, 10);
        memberBookings.TotalCount.Should().Be(1);

        var platformAction = new PlatformCorporateSsoActionDto("Reason");
        platformAction.Reason.Should().Be("Reason");

        // SSO DTOs
        var domainDto = new CompanySsoDomainDto(id, "corp.test", true, now, "txt_host", "txt_val");
        domainDto.Domain.Should().Be("corp.test");

        var ssoConfigDto = new CompanySsoConfigDto(companyId, "saml", true, false, true, "http://idp", "client", true, false, "http://redirect", true, true, false, false, now, "Success", new List<CompanySsoDomainDto> { domainDto });
        ssoConfigDto.Authority.Should().Be("http://idp");

        var platformSsoSummary = new PlatformCompanySsoSummaryDto(companyId, "Acme", "acme", true, true, false, true, null, null, "saml", "http://auth", now, "Success", 1, 10, now);
        platformSsoSummary.CompanyName.Should().Be("Acme");

        var ssoAuditDto = new CorporateSsoAuditEventDto(id, "sso.verify", "success", null, adminId, now, "detail");
        ssoAuditDto.Action.Should().Be("sso.verify");

        var upsertSso = new UpsertCompanySsoDto("saml", "http://idp", "client", "secret", "http://meta", "{}", "client_secret_post");
        upsertSso.Protocol.Should().Be("saml");

        var addDomain = new AddCompanySsoDomainDto("corp.test");
        addDomain.Domain.Should().Be("corp.test");

        var vendorAlloc = new VendorParkingAllocationDto(id, companyId, "Acme", Guid.NewGuid(), "Space", 10, 5, 5, 100m, now, now.AddMonths(1), AllocationStatus.Active, ParkingAllocationSource.VendorLease, Guid.NewGuid(), "REF", adminId, now, policy, now);
        vendorAlloc.ParkingSpaceTitle.Should().Be("Space");

        // Exercise Record Methods (ToString, GetHashCode, Equals, Operators)
        ExerciseRecord(compDto);
        ExerciseRecord(createComp);
        ExerciseRecord(updateComp);
        ExerciseRecord(memDto);
        ExerciseRecord(compMembers);
        ExerciseRecord(addMem);
        ExerciseRecord(updateMem);
        ExerciseRecord(inviteMem);
        ExerciseRecord(invDto);
        ExerciseRecord(pool);
        ExerciseRecord(policy);
        ExerciseRecord(fixedAssign);
        ExerciseRecord(assignFixed);
        ExerciseRecord(updateAlloc);
        ExerciseRecord(spaceDto);
        ExerciseRecord(updateSpaceDto);
        ExerciseRecord(corpBooking);
        ExerciseRecord(filter);
        ExerciseRecord(waitlistDto);
        ExerciseRecord(fraudAssess);
        ExerciseRecord(reservResult);
        ExerciseRecord(bookCorp);
        ExerciseRecord(bookVisitor);
        ExerciseRecord(dashDto);
        ExerciseRecord(genInv);
        ExerciseRecord(markPaid);
        ExerciseRecord(voidInv);
        ExerciseRecord(invLine);
        ExerciseRecord(invSummary);
        ExerciseRecord(invDetail);
        ExerciseRecord(invList);
        ExerciseRecord(memberBookings);
        ExerciseRecord(platformAction);
        ExerciseRecord(domainDto);
        ExerciseRecord(ssoConfigDto);
        ExerciseRecord(platformSsoSummary);
        ExerciseRecord(ssoAuditDto);
        ExerciseRecord(upsertSso);
        ExerciseRecord(addDomain);
        ExerciseRecord(vendorAlloc);
    }

    private static void ExerciseRecord<T>(T instance) where T : class
    {
        instance.ToString().Should().NotBeNullOrEmpty();
        instance.GetHashCode();
        instance.Equals(instance).Should().BeTrue();
        instance.Equals((object?)null).Should().BeFalse();
    }
}

