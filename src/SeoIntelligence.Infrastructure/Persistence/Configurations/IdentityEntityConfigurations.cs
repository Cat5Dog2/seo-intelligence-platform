using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeoIntelligence.Infrastructure.Identity;

namespace SeoIntelligence.Infrastructure.Persistence;

/// <summary>
/// Maps the ASP.NET Core Identity tables onto this database's snake_case naming convention.
/// The `identity_` prefix keeps them distinct from the business-level user tables that
/// docs/basic_design.md reserves for the Phase 4 multi-user extension.
/// </summary>
internal static class IdentityEntityConfigurations
{
    public static ModelBuilder ApplyIdentityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationRoleConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationUserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationUserClaimConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationUserLoginConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationUserTokenConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationRoleClaimConfiguration());

        return modelBuilder;
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.ToTable("identity_users");
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.UserName).HasColumnName("user_name");
        entity.Property(e => e.NormalizedUserName).HasColumnName("normalized_user_name");
        entity.Property(e => e.Email).HasColumnName("email");
        entity.Property(e => e.NormalizedEmail).HasColumnName("normalized_email");
        entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
        entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
        entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
        entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
        entity.Property(e => e.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        entity.Property(e => e.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        entity.Property(e => e.LockoutEnd).HasColumnName("lockout_end").HasColumnType("timestamptz");
        entity.Property(e => e.LockoutEnabled).HasColumnName("lockout_enabled");
        entity.Property(e => e.AccessFailedCount).HasColumnName("access_failed_count");
        entity.Property(e => e.DisplayName).HasColumnName("display_name");
        entity.Property(e => e.IsEnabled).HasColumnName("is_enabled");
        entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at").HasColumnType("timestamptz");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        entity.HasIndex(e => e.NormalizedUserName).IsUnique().HasDatabaseName("ux_identity_users_normalized_user_name");
        entity.HasIndex(e => e.NormalizedEmail).HasDatabaseName("ix_identity_users_normalized_email");
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> entity)
    {
        entity.ToTable("identity_roles");
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.Name).HasColumnName("name");
        entity.Property(e => e.NormalizedName).HasColumnName("normalized_name");
        entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");

        entity.HasIndex(e => e.NormalizedName).IsUnique().HasDatabaseName("ux_identity_roles_normalized_name");
    }
}

internal sealed class ApplicationUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> entity)
    {
        entity.ToTable("identity_user_roles");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.RoleId).HasColumnName("role_id");
    }
}

internal sealed class ApplicationUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> entity)
    {
        entity.ToTable("identity_user_claims");
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.ClaimType).HasColumnName("claim_type");
        entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
    }
}

internal sealed class ApplicationUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> entity)
    {
        entity.ToTable("identity_user_logins");
        entity.Property(e => e.LoginProvider).HasColumnName("login_provider");
        entity.Property(e => e.ProviderKey).HasColumnName("provider_key");
        entity.Property(e => e.ProviderDisplayName).HasColumnName("provider_display_name");
        entity.Property(e => e.UserId).HasColumnName("user_id");
    }
}

internal sealed class ApplicationUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> entity)
    {
        entity.ToTable("identity_user_tokens");
        entity.Property(e => e.UserId).HasColumnName("user_id");
        entity.Property(e => e.LoginProvider).HasColumnName("login_provider");
        entity.Property(e => e.Name).HasColumnName("name");
        entity.Property(e => e.Value).HasColumnName("value");
    }
}

internal sealed class ApplicationRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<string>> entity)
    {
        entity.ToTable("identity_role_claims");
        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.RoleId).HasColumnName("role_id");
        entity.Property(e => e.ClaimType).HasColumnName("claim_type");
        entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
    }
}
