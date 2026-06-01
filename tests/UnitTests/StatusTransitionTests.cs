using SeoIntelligence.Domain.Common;

namespace UnitTests;

public sealed class StatusTransitionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void LifecycleStatusAllowsSoftDeleteAndRestoreButRejectsArchivedToDisabled()
    {
        Assert.True(LifecycleStatusTransitions.CanTransition(LifecycleStatus.Active, LifecycleStatus.Archived));
        Assert.True(LifecycleStatusTransitions.CanTransition(LifecycleStatus.Active, LifecycleStatus.Disabled));
        Assert.True(LifecycleStatusTransitions.CanTransition(LifecycleStatus.Archived, LifecycleStatus.Active));
        Assert.True(LifecycleStatusTransitions.CanTransition(LifecycleStatus.Disabled, LifecycleStatus.Active));

        Assert.False(LifecycleStatusTransitions.CanTransition(LifecycleStatus.Archived, LifecycleStatus.Disabled));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void JobStatusAllowsWaitingExternalCancellationAndKeepsTerminalStatesClosed()
    {
        Assert.True(JobStatusTransitions.CanTransition(JobStatus.Queued, JobStatus.Running));
        Assert.True(JobStatusTransitions.CanTransition(JobStatus.Running, JobStatus.WaitingExternal));
        Assert.True(JobStatusTransitions.CanCancel(JobStatus.Queued));
        Assert.True(JobStatusTransitions.CanCancel(JobStatus.WaitingExternal));

        Assert.False(JobStatusTransitions.CanCancel(JobStatus.Running));
        Assert.False(JobStatusTransitions.CanCancel(JobStatus.FailedRetryable));
        Assert.False(JobStatusTransitions.CanTransition(JobStatus.Succeeded, JobStatus.Queued));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(400, JobStatus.FailedFatal)]
    [InlineData(402, JobStatus.FailedFatal)]
    [InlineData(403, JobStatus.FailedFatal)]
    [InlineData(429, JobStatus.FailedRetryable)]
    [InlineData(500, JobStatus.FailedRetryable)]
    [InlineData(503, JobStatus.FailedRetryable)]
    public void JobFailureClassifierMatchesRetryPolicy(int statusCode, JobStatus expected)
    {
        var actual = JobFailureClassifier.FromHttpStatusCode(statusCode);

        Assert.Equal(expected, actual);
    }
}
