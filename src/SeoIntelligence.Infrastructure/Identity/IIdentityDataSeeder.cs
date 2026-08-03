namespace SeoIntelligence.Infrastructure.Identity;

public interface IIdentityDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
