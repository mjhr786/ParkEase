namespace ParkingApp.Application.Interfaces;

public interface ICorporateTenantContext
{
    Guid? CompanyId { get; }
    void SetCompanyId(Guid companyId);
}
