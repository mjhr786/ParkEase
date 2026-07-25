namespace ParkingApp.Corporate.Contracts;

public interface ICorporateTenantContext
{
    Guid? CompanyId { get; }
    void SetCompanyId(Guid companyId);
}
