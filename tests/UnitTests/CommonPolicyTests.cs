using SeoIntelligence.Application.ProjectContext;
using SeoIntelligence.Domain.Common;

namespace UnitTests;

public sealed class CommonPolicyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ProjectContextServiceUsesFixedDeveloperActorAndUtcTimestamp()
    {
        var workspaceId = Guid.Parse("018f26ab-3f8d-7b4a-8bfb-768ab975f111");
        var projectId = Guid.Parse("018f26ab-3f8d-7b4a-8bfb-768ab975f222");
        var service = new ProjectContextService(TimeProvider.System);

        var context = service.Create(workspaceId, projectId, " correlation-1 ");

        Assert.Equal(SystemActor.Developer, context.Actor);
        Assert.Equal(TimeSpan.Zero, context.RequestedAtUtc.Offset);
        Assert.Equal("correlation-1", context.CorrelationId);
        Assert.True(service.IsInProjectScope(context, projectId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UuidV7CreatesVersionSevenGuid()
    {
        var id = UuidV7.New(DateTimeOffset.Parse("2026-05-31T00:00:00Z"));

        Assert.Equal('7', id.ToString("N")[12]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TokyoDateBoundaryUsesTokyoMidnightInUtc()
    {
        var window = TokyoDateBoundary.DayWindow(DateTimeOffset.Parse("2026-05-30T18:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2026-05-30T15:00:00Z"), window.StartUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-05-31T15:00:00Z"), window.EndUtc);
    }
}
