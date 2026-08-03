using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeoIntelligence.Application.Auditing;
using SeoIntelligence.Application.Jobs;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Domain.Normalization;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class AiAssistantIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Security")]
    public async Task AiChatQueuesJobGeneratesDraftAndStoresReviewableHistoryWithoutSecrets()
    {
        await using var factory = new AiAssistantApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithAiReferenceDataAsync(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/ai/chat",
            new
            {
                message = """
                Please summarize rank results, rewrite gaps, and the latest report.
                apiKey=rk_live_123456789
                Contact owner@example.com before publishing.
                """,
                allowedTools = new[] { "rank-results", "rewrite-analysis", "report-summary" },
                referenceScope = new
                {
                    projectOnly = true,
                    webhook = "https://discord.com/api/webhooks/123456789/abcdef"
                }
            });
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var data = document.RootElement.GetProperty("data");
        var jobId = data.GetProperty("jobId").GetGuid();
        var messageId = data.GetProperty("messageId").GetGuid();
        Assert.Equal(jobId, document.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());
        Assert.Equal("redacted", data.GetProperty("redactionStatus").GetString());
        Assert.Equal(StatusValues.Pending, data.GetProperty("reviewStatus").GetString());
        Assert.Contains("queued", data.GetProperty("response").GetString(), StringComparison.OrdinalIgnoreCase);

        await DispatchAsync(factory, jobId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
        var message = await dbContext.AiMessages.AsNoTracking().SingleAsync(entity => entity.Id == messageId);
        var session = await dbContext.AiSessions.AsNoTracking().SingleAsync(entity => entity.Id == message.SessionId);
        var version = await dbContext.ArtifactVersions.AsNoTracking().SingleAsync(entity =>
            entity.ArtifactType == "ai_message" &&
            entity.ArtifactId == messageId);

        Assert.Equal(StatusValues.Succeeded, job.Status);
        Assert.Equal(100, job.Progress);
        Assert.Equal(projectId, session.ProjectId);
        Assert.Equal("developer", session.Actor);
        Assert.Equal("redacted", message.RedactionStatus);
        Assert.Equal(StatusValues.Pending, message.ReviewStatus);
        Assert.Contains("Draft AI response", message.Response, StringComparison.Ordinal);
        Assert.DoesNotContain("rk_live_123456789", message.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("owner@example.com", message.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatusValues.Pending, version.ReviewStatus);

        using var toolCalls = JsonDocument.Parse(message.ToolCallsJson);
        var toolNames = toolCalls.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("rank-results", toolNames);
        Assert.Contains("rewrite-analysis", toolNames);
        Assert.Contains("report-summary", toolNames);

        using var referenceData = JsonDocument.Parse(message.ReferenceDataJson);
        Assert.Equal("AI Reference Project", referenceData.RootElement.GetProperty("project").GetProperty("name").GetString());
        Assert.True(referenceData.RootElement.GetProperty("topKeywords").GetArrayLength() >= 1);
        Assert.True(referenceData.RootElement.GetProperty("rewriteTasks").GetArrayLength() >= 1);
        Assert.True(referenceData.RootElement.GetProperty("reports").GetArrayLength() >= 1);
        var redactedScope = referenceData.RootElement
            .GetProperty("request")
            .GetProperty("referenceScope")
            .GetProperty("redactedJson")
            .GetString();
        Assert.DoesNotContain("discord.com/api/webhooks", redactedScope, StringComparison.OrdinalIgnoreCase);

        using var tokenUsage = JsonDocument.Parse(message.TokenUsage);
        Assert.True(tokenUsage.RootElement.GetProperty("totalTokens").GetInt32() > 0);

        var aiAudits = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entity => entity.ResourceType == AuditLogResourceTypes.AiMessage && entity.ResourceId == messageId.ToString("D"))
            .Select(entity => new { entity.Action, entity.BeforeAfterJson })
            .ToArrayAsync();
        Assert.Contains(aiAudits, audit => audit.Action == AuditLogActionNames.AiChatQueued);
        Assert.Contains(aiAudits, audit => audit.Action == AuditLogActionNames.AiChatCompleted);
        Assert.All(aiAudits, audit =>
        {
            Assert.DoesNotContain("rk_live_123456789", audit.BeforeAfterJson, StringComparison.Ordinal);
            Assert.DoesNotContain("owner@example.com", audit.BeforeAfterJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("discord.com/api/webhooks", audit.BeforeAfterJson, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AiChatRejectsEmptyMessageAndUnsupportedTools()
    {
        await using var factory = new AiAssistantApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectWithAiReferenceDataAsync(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/ai/chat",
            new
            {
                message = " ",
                allowedTools = new[] { "raw-secret-reader" }
            });
        using var document = await ReadJsonAsync(response);
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errors, error => error.GetProperty("target").GetString() == "message");
        Assert.Contains(errors, error => error.GetProperty("target").GetString() == "allowedTools");
        Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));
    }

    private static async Task DispatchAsync(AiAssistantApiFactory factory, Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        await dispatcher.DispatchAsync(jobId);
    }

    private static async Task<Guid> SeedProjectWithAiReferenceDataAsync(AiAssistantApiFactory factory)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var keywordId = Guid.NewGuid();

        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = "AI Reference Project",
            DefaultLocation = "JP",
            DefaultLanguage = "ja",
            KpiJson = "{}",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.Keywords.Add(new KeywordEntity
        {
            Id = keywordId,
            NormalizedText = "ai seo assistant",
            Language = "ja",
            TextHash = HashText("ai seo assistant"),
            CreatedAt = now
        });
        dbContext.ProjectKeywordScores.Add(new ProjectKeywordScoreEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            Location = "JP",
            Language = "ja",
            OpportunityScore = 82m,
            ScoreComponentsJson = "{}",
            ScoredAt = now
        });
        dbContext.RewriteTasks.Add(new RewriteTaskEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TargetUrl = "https://example.com/ai-seo",
            PriorityScore = 76m,
            ReasonJson = """{ "reason": "ranking decline" }""",
            Status = StatusValues.Active,
            AssigneeActor = "developer",
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.CannibalizationCandidates.Add(new CannibalizationCandidateEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            PrimaryUrl = "https://example.com/ai-seo",
            CompetingUrlsJson = """[{ "url": "https://example.com/ai-seo-guide" }]""",
            SeverityScore = 61m,
            EvidenceJson = "{}",
            RecommendationJson = """{ "action": "merge" }""",
            Status = StatusValues.Active,
            DetectedAt = now
        });
        dbContext.Reports.Add(new ReportEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReportType = "monthly",
            Period = "2026-05",
            Format = "pdf",
            CurrentVersion = 1,
            FileUri = "storage://local/reports/example.pdf",
            Status = "completed",
            GeneratedBy = "developer",
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.ArticleBriefs.Add(new ArticleBriefEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "AI SEO assistant article",
            CurrentVersion = 1,
            ContentJson = "{}",
            ReviewStatus = StatusValues.Pending,
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static HttpClient CreateClient(AiAssistantApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Expected JSON response. Status: {(int)response.StatusCode} {response.StatusCode}.");
        return JsonDocument.Parse(content);
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(KeywordNormalizer.Normalize(value)))).ToLowerInvariant();

    private sealed class AiAssistantApiFactory : ServiceKeyApiFactory
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "",
                    ["Redis:ConnectionString"] = "",
                    ["Storage:Provider"] = "Local",
                    ["Storage:BasePath"] = Path.Combine(Path.GetTempPath(), "seo-intelligence-ai-tests", databaseName),
                    ["Storage:BucketName"] = "seo-intelligence",
                    ["SecretStore:Provider"] = "Configuration",
                    ["SecretStore:ConfigurationPrefix"] = "Secrets",
                    ["Hangfire:Storage"] = "PostgreSQL",
                    ["OpenTelemetry:ServiceName"] = "IntegrationTests",
                    ["RakkoKeyword:Mode"] = "Mock"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<SeoIntelligenceDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }
}
