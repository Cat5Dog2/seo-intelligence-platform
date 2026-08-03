using Microsoft.AspNetCore.Identity;
using SeoIntelligence.Application.Security;
using SeoIntelligence.Infrastructure.Identity;

namespace ContractTests;

public sealed class IdentityPolicyContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void PasswordPolicyRequiresTwelveCharactersAcrossAllCharacterClasses()
    {
        var options = new IdentityOptions();

        SeoIntelligenceIdentityOptions.Configure(options);

        Assert.Equal(12, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireNonAlphanumeric);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void LockoutLocksTheAccountAfterFiveFailedAttemptsForFifteenMinutes()
    {
        var options = new IdentityOptions();

        SeoIntelligenceIdentityOptions.Configure(options);

        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void SignInDoesNotRequireAccountConfirmationButRequiresUniqueEmail()
    {
        var options = new IdentityOptions();

        SeoIntelligenceIdentityOptions.Configure(options);

        Assert.False(options.SignIn.RequireConfirmedAccount);
        Assert.True(options.User.RequireUniqueEmail);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void AdminRoleIsPartOfTheSeededRoleSet()
    {
        Assert.Contains(ApplicationRoles.Admin, ApplicationRoles.All);
        Assert.Contains(ApplicationRoles.User, ApplicationRoles.All);
    }
}
