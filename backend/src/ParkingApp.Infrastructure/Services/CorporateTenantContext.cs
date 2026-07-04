namespace ParkingApp.Infrastructure.Services;

using ParkingApp.Application.Interfaces;

public class CorporateTenantContext : ICorporateTenantContext
{
    public Guid? CompanyId { get; private set; }

    public void SetCompanyId(Guid companyId)
    {
        if (CompanyId.HasValue && CompanyId.Value != companyId)
        {
            throw new InvalidOperationException("Tenant Context CompanyId is already set and cannot be changed during the request lifecycle.");
        }
        
        CompanyId = companyId;
    }
}
