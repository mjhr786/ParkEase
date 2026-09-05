using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CompanySsoConfigurationConfiguration : IEntityTypeConfiguration<CompanySsoConfiguration>
{
    public void Configure(EntityTypeBuilder<CompanySsoConfiguration> entity)
    {
        entity.ToTable("CompanySsoConfigurations");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Protocol).HasConversion<short>().IsRequired();
        entity.Property(e => e.Authority).HasMaxLength(512).IsRequired();
        entity.Property(e => e.ClientId).HasMaxLength(256).IsRequired();
        entity.Property(e => e.ClientSecretProtected).HasColumnType("text");
        entity.Property(e => e.MetadataUrl).HasMaxLength(512);
        entity.Property(e => e.AttributeMappingJson).HasColumnType("text");
        entity.Property(e => e.TokenEndpointAuthMethod).HasMaxLength(32);
        entity.Property(e => e.SpEntityId).HasMaxLength(512);
        entity.Property(e => e.AcsUrlOverride).HasMaxLength(512);
        entity.Property(e => e.IdpSigningCertProtected).HasColumnType("text");
        entity.Property(e => e.IdpSigningCertSecondaryProtected).HasColumnType("text");
        entity.Property(e => e.LastTestResult).HasMaxLength(64);

        entity.Property(e => e.IsEnabled).HasDefaultValue(false);
        entity.Property(e => e.ForceDisabledByPlatform).HasDefaultValue(false);
        entity.Property(e => e.PasswordLoginAllowed).HasDefaultValue(true);
        entity.Property(e => e.AutoAcceptInvitationsOnSso).HasDefaultValue(true);
        entity.Property(e => e.ForceAuthn).HasDefaultValue(false);
        entity.Property(e => e.AllowJitProvisioning).HasDefaultValue(false);
        entity.Property(e => e.AllowIdpInitiated).HasDefaultValue(false);

        entity.HasIndex(e => e.CompanyId).IsUnique();
        entity.HasIndex(e => e.IsEnabled);

        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.Company)
            .WithOne(c => c.SsoConfiguration)
            .HasForeignKey<CompanySsoConfiguration>(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(e => e.Domains)
            .WithOne(d => d.Configuration)
            .HasForeignKey(d => d.CompanySsoConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
