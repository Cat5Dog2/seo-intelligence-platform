using SeoIntelligence.Application.Configuration;
using SeoIntelligence.Application.Security;

namespace UnitTests;

public sealed class SafeReturnUrlTests
{
    [Theory]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("/keywords?page=2", "/keywords?page=2")]
    [InlineData("/", "/")]
    [Trait("Category", "Unit")]
    public void ResolveKeepsSameSiteAbsolutePaths(string returnUrl, string expected)
        => Assert.Equal(expected, SafeReturnUrl.Resolve(returnUrl));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void ResolveFallsBackToRootWhenReturnUrlIsMissing(string? returnUrl)
        => Assert.Equal(SafeReturnUrl.Fallback, SafeReturnUrl.Resolve(returnUrl));

    [Theory]
    [InlineData("//evil.example.com")]
    [InlineData("https://evil.example.com/dashboard")]
    [InlineData("http://evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("dashboard")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/redirect?to=https://evil.example.com")]
    [Trait("Category", "Unit")]
    public void ResolveRejectsReturnUrlsThatCanLeaveTheSite(string returnUrl)
        => Assert.Equal(SafeReturnUrl.Fallback, SafeReturnUrl.Resolve(returnUrl));
}

public sealed class ServiceAuthenticationOptionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void DefaultOptionsAreValid()
    {
        var options = new ServiceAuthenticationOptions();

        Assert.Equal(ServiceAuthenticationOptions.DefaultServiceKeyRef, options.ServiceKeyRef);
        Assert.Empty(options.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void ValidateRequiresAServiceKeyReference(string serviceKeyRef)
    {
        var options = new ServiceAuthenticationOptions { ServiceKeyRef = serviceKeyRef };

        var errors = options.Validate();

        Assert.Contains(errors, error => error.Contains(nameof(ServiceAuthenticationOptions.ServiceKeyRef), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HeaderNameMatchesTheDocumentedContract()
        => Assert.Equal("X-Service-Key", ServiceAuthenticationOptions.HeaderName);
}
