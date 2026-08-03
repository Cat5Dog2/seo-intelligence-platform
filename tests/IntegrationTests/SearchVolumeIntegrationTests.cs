using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeoIntelligence.Application.RakkoKeyword;
using SeoIntelligence.Domain.Common;
using SeoIntelligence.Infrastructure.Persistence;
using SeoIntelligence.Infrastructure.Persistence.Entities;
using SeoIntelligence.Infrastructure.Services;

using IntegrationTests.Support;

namespace IntegrationTests;

public sealed class SearchVolumeIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeJobRegistersPollsAndPersistsMoreThanThousandResults()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            var keywords = Enumerable.Range(1, 1005)
                .Select(index => $" keyword {index:D4} ")
                .Concat(["keyword 0001", "", "keyword 0002"])
                .ToArray();

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords,
                    location = "Japan",
                    language = "Japanese",
                    aggregationPeriodMonths = 12,
                    seoDifficulty = true
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.Equal(jobId, document.RootElement.GetProperty("meta").GetProperty("jobId").GetGuid());

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var searchVolumeJob = await dbContext.SearchVolumeJobs.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
                using var options = JsonDocument.Parse(searchVolumeJob.RequestOptionsJson);
                Assert.Equal(1005, options.RootElement.GetProperty("normalizedKeywordCount").GetInt32());
                Assert.Equal(783.9m, options.RootElement.GetProperty("estimatedCredit").GetDecimal());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);

                Assert.Equal(StatusValues.WaitingExternal, job.Status);
                Assert.Equal("7000001", externalRequest.ExternalRequestId);
                Assert.Equal(StatusValues.WaitingExternal, externalRequest.Status);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);
                var externalRequest = await dbContext.JobExternalRequests.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);

                Assert.Equal(StatusValues.Succeeded, job.Status);
                Assert.Equal(100, job.Progress);
                Assert.Equal(StatusValues.Succeeded, externalRequest.Status);
                Assert.Equal(1005, await dbContext.SearchVolumeResults.CountAsync(entity => entity.JobId == jobId));
                Assert.Equal(1005, await dbContext.KeywordMetrics.CountAsync());
                Assert.Equal(2010, await dbContext.KeywordMonthlyVolumes.CountAsync());
                Assert.Equal(1005, await dbContext.ProjectKeywordScores.CountAsync(entity => entity.ProjectId == projectId));

                var topScore = await dbContext.ProjectKeywordScores
                    .AsNoTracking()
                    .OrderByDescending(entity => entity.OpportunityScore)
                    .FirstAsync(entity => entity.ProjectId == projectId);
                using var components = JsonDocument.Parse(topScore.ScoreComponentsJson);
                Assert.True(topScore.OpportunityScore > 0m);
                Assert.True(components.RootElement.TryGetProperty("volumeScore", out _));
                Assert.True(components.RootElement.TryGetProperty("difficultyScore", out _));
                Assert.True(components.RootElement.TryGetProperty("trendScore", out _));
                Assert.True(components.RootElement.TryGetProperty("commercialScore", out _));
                Assert.True(components.RootElement.TryGetProperty("relevanceScore", out _));
                Assert.True(components.RootElement.TryGetProperty("sourceCallId", out _));
            }

            using var resultsResponse = await client.GetAsync(
                $"/api/projects/{projectId}/search-volume/jobs/{jobId}/results?page=1&pageSize=5&sortBy=searchVolume&orderBy=desc");
            using var resultsDocument = await ReadJsonAsync(resultsResponse);

            Assert.Equal(HttpStatusCode.OK, resultsResponse.StatusCode);
            Assert.Equal(1005, resultsDocument.RootElement.GetProperty("meta").GetProperty("page").GetProperty("totalCount").GetInt64());
            var rows = resultsDocument.RootElement.GetProperty("data").EnumerateArray().ToArray();
            Assert.Equal(5, rows.Length);
            Assert.Equal("keyword 1005", rows[0].GetProperty("keyword").GetString());
            Assert.Equal(2005, rows[0].GetProperty("searchVolume").GetInt32());
            Assert.True(rows[0].GetProperty("monthlySearchVolume").TryGetProperty("2026-05", out _));

            await SeedDashboardFailureAndCreditAsync(factory, projectId);
            await SeedDashboardPhase2DataAsync(factory, projectId);

            using var dashboardResponse = await client.GetAsync($"/api/projects/{projectId}/dashboard");
            using var dashboardDocument = await ReadJsonAsync(dashboardResponse);

            Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
            var dashboard = dashboardDocument.RootElement.GetProperty("data");
            Assert.Equal(1, dashboard.GetProperty("searchVolumeJobCount").GetInt32());
            Assert.Equal(1005, dashboard.GetProperty("searchVolumeResultCount").GetInt32());
            Assert.Equal(1005, dashboard.GetProperty("opportunityScoreCount").GetInt32());
            Assert.Equal(7, dashboard.GetProperty("consumedCredit").GetInt32());
            Assert.Equal(1, dashboard.GetProperty("failedJobCount").GetInt32());
            Assert.Equal(1, dashboard.GetProperty("notificationFailureCount").GetInt32());
            var topOpportunityScores = dashboard.GetProperty("topOpportunityScores").EnumerateArray().ToArray();
            Assert.Equal(10, topOpportunityScores.Length);
            Assert.False(string.IsNullOrWhiteSpace(topOpportunityScores[0].GetProperty("keyword").GetString()));
            Assert.True(topOpportunityScores[0].GetProperty("opportunityScore").GetDecimal() > 0m);

            var competitorSummary = dashboard.GetProperty("competitorSummary");
            Assert.Equal(1, competitorSummary.GetProperty("competitorCount").GetInt32());
            Assert.Equal(1, competitorSummary.GetProperty("savedCompetitorCount").GetInt32());
            Assert.Equal(0.5m, competitorSummary.GetProperty("averageDuplicateRate").GetDecimal());
            Assert.Equal(300m, competitorSummary.GetProperty("trafficValue").GetDecimal());

            var influxSummary = dashboard.GetProperty("influxSummary");
            Assert.Equal(2, influxSummary.GetProperty("keywordCount").GetInt32());
            Assert.Equal(1, influxSummary.GetProperty("gapKeywordCount").GetInt32());
            Assert.Equal(1, influxSummary.GetProperty("pageCount").GetInt32());

            var contentSummary = dashboard.GetProperty("contentAnalysisSummary");
            Assert.Equal(1, contentSummary.GetProperty("keywordCount").GetInt32());
            Assert.Equal(1, contentSummary.GetProperty("contentResultCount").GetInt32());
            Assert.Equal(1, contentSummary.GetProperty("headlinePageCount").GetInt32());
            Assert.Equal(1, contentSummary.GetProperty("coOccurrenceWordCount").GetInt32());

            var briefSummary = dashboard.GetProperty("briefSummary");
            Assert.Equal(2, briefSummary.GetProperty("briefCount").GetInt32());
            Assert.Equal(1, briefSummary.GetProperty("draftCount").GetInt32());
            Assert.Equal(1, briefSummary.GetProperty("pendingReviewCount").GetInt32());
            Assert.Equal(1, briefSummary.GetProperty("reviewedCount").GetInt32());

            var rankSummary = dashboard.GetProperty("rankSummary");
            Assert.Equal(1, rankSummary.GetProperty("rankCheckJobCount").GetInt32());
            Assert.Equal(2, rankSummary.GetProperty("rankResultCount").GetInt32());
            Assert.Equal(1, rankSummary.GetProperty("distribution").GetProperty("top3").GetInt32());
            Assert.Equal(1, rankSummary.GetProperty("distribution").GetProperty("top20").GetInt32());

            var rankAlertSummary = dashboard.GetProperty("rankAlertSummary");
            Assert.Equal(1, rankAlertSummary.GetProperty("activeAlertCount").GetInt32());
            Assert.Equal(1, rankAlertSummary.GetProperty("unresolvedEventCount").GetInt32());
            Assert.Equal(1, rankAlertSummary.GetProperty("rankAlertNotificationCount").GetInt32());
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeCanceledWaitingExternalJobDoesNotIngestResults()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo", "content marketing" },
                    location = "Japan",
                    language = "Japanese",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            using (var cancelResponse = await client.PostAsync($"/api/jobs/{jobId}/cancel", content: null))
            using (var cancelDocument = await ReadJsonAsync(cancelResponse))
            {
                Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
                Assert.Equal(StatusValues.Canceled, cancelDocument.RootElement.GetProperty("data").GetProperty("status").GetString());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                Assert.Equal(0, await dbContext.SearchVolumeResults.CountAsync(entity => entity.JobId == jobId));
                Assert.Equal(0, await dbContext.KeywordMetrics.CountAsync());
                Assert.Equal(0, factory.RakkoKeywordClient.ResultsCallCount);
            }
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationNormalizesLegacyLocationAndLanguageCodes()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            await SeedMasterDataAsync(factory);

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "2392",
                    language = "ja",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            var searchVolumeJob = await dbContext.SearchVolumeJobs.AsNoTracking().SingleAsync(entity => entity.JobId == jobId);
            Assert.Equal("Japan", searchVolumeJob.Location);
            Assert.Equal("Japanese", searchVolumeJob.Language);
            using var options = JsonDocument.Parse(searchVolumeJob.RequestOptionsJson);
            Assert.Equal("Japan", options.RootElement.GetProperty("location").GetString());
            Assert.Equal("Japanese", options.RootElement.GetProperty("language").GetString());
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationTreatsAliasesCaseDifferencesAndCanonicalNamesAsSameJob()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            await SeedMasterDataAsync(factory);
            var inputs = new[]
            {
                (Location: "JP", Language: "Japanese"),
                (Location: "JP", Language: "ja"),
                (Location: "japan", Language: "japanese"),
                (Location: "2392", Language: "ja"),
                (Location: "Japan", Language: "Japanese"),
                (Location: "Japan", Language: "Japanese")
            };
            var jobIds = new List<Guid>();

            foreach (var input in inputs)
            {
                using var response = await client.PostAsJsonAsync(
                    $"/api/projects/{projectId}/search-volume/jobs",
                    new
                    {
                        keywords = new[] { "seo" },
                        location = input.Location,
                        language = input.Language,
                        aggregationPeriodMonths = 12
                    });
                using var document = await ReadJsonAsync(response);

                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                jobIds.Add(document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid());
            }

            Assert.Single(jobIds.Distinct());
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            Assert.Equal(1, await dbContext.Jobs.CountAsync(entity => entity.ProjectId == projectId));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationCreatesCanonicalJobAfterLegacyJobWasCanceled()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            using var legacyResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "JP",
                    language = "ja",
                    aggregationPeriodMonths = 12
                });
            using var legacyDocument = await ReadJsonAsync(legacyResponse);
            Assert.Equal(HttpStatusCode.Accepted, legacyResponse.StatusCode);
            var legacyJobId = legacyDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var legacyJob = await dbContext.Jobs.SingleAsync(entity => entity.Id == legacyJobId);
                legacyJob.Status = StatusValues.Canceled;
                legacyJob.CompletedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            await SeedMasterDataAsync(factory);
            using var canonicalResponse = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "Japan",
                    language = "Japanese",
                    aggregationPeriodMonths = 12
                });
            using var canonicalDocument = await ReadJsonAsync(canonicalResponse);

            Assert.Equal(HttpStatusCode.Accepted, canonicalResponse.StatusCode);
            var canonicalJobId = canonicalDocument.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();
            Assert.NotEqual(legacyJobId, canonicalJobId);
            await using var verificationScope = factory.Services.CreateAsyncScope();
            var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            Assert.Equal(2, await verificationDbContext.Jobs.CountAsync(entity => entity.ProjectId == projectId));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationRejectsLegacyCodesWhoseNamesAreInactive()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            await SeedMasterDataAsync(factory, activeLocationName: "Canada", activeLanguageName: "English");

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "2392",
                    language = "ja",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var targets = document.RootElement
                .GetProperty("errors")
                .EnumerateArray()
                .Select(error => error.GetProperty("target").GetString())
                .ToArray();
            Assert.Contains("location", targets);
            Assert.Contains("language", targets);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationRejectsValuesWhenSynchronizedMasterHasNoActiveEntries()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            await SeedMasterDataAsync(factory, activeLocationName: null, activeLanguageName: null);

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "Japan",
                    language = "Japanese",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var targets = document.RootElement
                .GetProperty("errors")
                .EnumerateArray()
                .Select(error => error.GetProperty("target").GetString())
                .ToArray();
            Assert.Contains("location", targets);
            Assert.Contains("language", targets);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationSkipsMasterValidationOnlyWhenProviderHasNoEntries()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "Unlisted location",
                    language = "Unlisted language",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            Assert.Equal(1, await dbContext.Jobs.CountAsync(entity => entity.ProjectId == projectId));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeRegistrationRejectsUnknownLocationAndLanguageWhenMasterIsSynchronized()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);

        try
        {
            await SeedMasterDataAsync(factory);

            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "Atlantis",
                    language = "Klingon",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errors = document.RootElement.GetProperty("errors").EnumerateArray().ToArray();
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "location");
            Assert.Contains(errors, error => error.GetProperty("target").GetString() == "language");
            Assert.All(errors, error => Assert.Equal("Validation.Failed", error.GetProperty("code").GetString()));

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
            Assert.Equal(0, await dbContext.Jobs.CountAsync(entity => entity.ProjectId == projectId));
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchVolumeInvalidExternalResponseFailsFatallyWithoutRetryingBilledRegistration()
    {
        await using var factory = new SearchVolumeApiFactory();
        using var client = CreateClient(factory);
        var projectId = await SeedProjectAsync(factory);
        factory.RakkoKeywordClient.FailRegistrationWithInvalidResponse = true;

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/projects/{projectId}/search-volume/jobs",
                new
                {
                    keywords = new[] { "seo" },
                    location = "Japan",
                    language = "Japanese",
                    aggregationPeriodMonths = 12
                });
            using var document = await ReadJsonAsync(response);
            var jobId = document.RootElement.GetProperty("data").GetProperty("jobId").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                var job = await dbContext.Jobs.AsNoTracking().SingleAsync(entity => entity.Id == jobId);

                // 課金される登録POSTは契約違反レスポンスでは再試行しない。
                Assert.Equal(StatusValues.FailedFatal, job.Status);
                Assert.Equal(0, job.RetryCount);
                Assert.Null(job.NextRunAt);
                Assert.NotNull(job.CompletedAt);

                using var error = JsonDocument.Parse(job.ErrorJson!);
                Assert.Equal("invalid_response", error.RootElement.GetProperty("errorCode").GetString());
                Assert.False(error.RootElement.GetProperty("retryable").GetBoolean());
            }

            Assert.Equal(1, factory.RakkoKeywordClient.RegisterCallCount);

            // 再ディスパッチしても終端ジョブは外部APIを再度呼ばない。
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
                await dispatcher.DispatchAsync(jobId);
            }

            Assert.Equal(1, factory.RakkoKeywordClient.RegisterCallCount);
        }
        finally
        {
            DeleteTempStoragePath(factory.StoragePath);
        }
    }

    private static async Task SeedMasterDataAsync(
        SearchVolumeApiFactory factory,
        string? activeLocationName = "Japan",
        string? activeLanguageName = "Japanese")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var locations = new List<LocationEntity>
        {
            new LocationEntity
            {
                Id = Guid.NewGuid(),
                Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                LocationCode = "2392",
                LocationName = "Japan",
                CountryCode = "JP",
                Status = StatusValues.Archived,
                SyncedAt = DateTime.UtcNow
            }
        };
        if (activeLocationName is not null)
        {
            locations.Add(
            new LocationEntity
            {
                Id = Guid.NewGuid(),
                Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                LocationCode = activeLocationName,
                LocationName = activeLocationName,
                CountryCode = activeLocationName == "Japan" ? "JP" : "CA",
                Status = StatusValues.Active,
                SyncedAt = DateTime.UtcNow
            });
        }

        var languages = new List<LanguageEntity>
        {
            new LanguageEntity
            {
                Id = Guid.NewGuid(),
                Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                LanguageCode = "ja",
                LanguageName = "Japanese",
                Status = StatusValues.Archived,
                SyncedAt = DateTime.UtcNow
            }
        };
        if (activeLanguageName is not null)
        {
            languages.Add(
            new LanguageEntity
            {
                Id = Guid.NewGuid(),
                Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
                LanguageCode = activeLanguageName,
                LanguageName = activeLanguageName,
                Status = StatusValues.Active,
                SyncedAt = DateTime.UtcNow
            });
        }

        dbContext.Locations.AddRange(locations);
        dbContext.Languages.AddRange(languages);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> SeedProjectAsync(SearchVolumeApiFactory factory)
    {
        var projectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        dbContext.Projects.Add(new ProjectEntity
        {
            Id = projectId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            Name = $"Search Volume {projectId:N}",
            DefaultLocation = "Japan",
            DefaultLanguage = "Japanese",
            KpiJson = "{}",
            Status = StatusValues.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static async Task SeedDashboardFailureAndCreditAsync(SearchVolumeApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var channelId = Guid.NewGuid();
        dbContext.NotificationChannels.Add(new NotificationChannelEntity
        {
            Id = channelId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelType = "discord",
            Name = "Dashboard failure test",
            WebhookSecretRef = "secret/ref",
            EventTypesJson = """["job_failed"]""",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.NotificationDeliveries.Add(new NotificationDeliveryEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelId = channelId,
            EventType = "job_failed",
            PayloadHash = "payload-hash",
            Status = StatusValues.Failed,
            ErrorMessage = "delivery failed",
            RetryCount = 1,
            CreatedAt = now
        });
        dbContext.ExternalApiCalls.Add(new ExternalApiCallEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            Provider = SeoIntelligenceSeedData.RakkoKeywordProvider,
            Endpoint = "/v1/search-volume",
            RequestHash = "dashboard-credit-request-hash",
            RequestUri = "storage://local/dashboard-credit-request.json.gz",
            ResponseHash = "dashboard-credit-response-hash",
            ResponseUri = "storage://local/dashboard-credit-response.json.gz",
            ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
            CacheHit = false,
            StatusCode = 200,
            ConsumedCredit = 7m,
            DurationMs = 120,
            Actor = "developer",
            RetainedUntil = now.AddMonths(3),
            CreatedAt = now
        });
        dbContext.Jobs.Add(new JobEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            JobType = "KeywordDiscoveryJob",
            Status = StatusValues.FailedFatal,
            Progress = 50,
            RetryCount = 0,
            RequestedBy = "developer",
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDashboardPhase2DataAsync(SearchVolumeApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
        var now = DateTime.UtcNow;
        var keywordId = Guid.NewGuid();
        var gapKeywordId = Guid.NewGuid();
        var rankJobId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        dbContext.Sites.Add(new SiteEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Domain = "own.example",
            CanonicalUrl = "https://own.example/",
            Type = "own",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.CompetitiveResults.Add(new CompetitiveResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SiteDomain = "competitor.example",
            DuplicateRate = 0.5m,
            EstimatedTraffic = 120m,
            TrafficValue = 300m,
            KeywordCount = 40,
            UniqueCountsJson = "{}",
            CreatedAt = now
        });
        dbContext.CompetitorSites.Add(new CompetitorSiteEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Domain = "competitor.example",
            Source = "competitive",
            DuplicateRate = 0.5m,
            EstimatedTraffic = 120m,
            CreatedAt = now
        });
        dbContext.InfluxKeywordResults.AddRange(
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "own.example",
                KeywordId = keywordId,
                Rank = 2,
                RankedUrl = "https://own.example/seo",
                EstimatedTraffic = 10m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            },
            new InfluxKeywordResultEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Target = "competitor.example",
                KeywordId = gapKeywordId,
                Rank = 4,
                RankedUrl = "https://competitor.example/seo",
                EstimatedTraffic = 20m,
                MetricsSnapshotJson = "{}",
                CreatedAt = now
            });
        dbContext.InfluxPageResults.Add(new InfluxPageResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Target = "competitor.example",
            PageUrl = "https://competitor.example/seo",
            Title = "Competitor SEO",
            KeywordCount = 12,
            EstimatedTraffic = 30m,
            TrafficValue = 80m,
            TopKeywordId = gapKeywordId,
            CreatedAt = now
        });
        dbContext.ContentSearchResults.Add(new ContentSearchResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            Url = "https://content.example/seo",
            Domain = "content.example",
            Title = "SEO content",
            Description = "SEO description",
            EstimatedTraffic = 15m,
            TrafficValue = 25m,
            CreatedAt = now
        });
        dbContext.SerpHeadlinePages.Add(new SerpHeadlinePageEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            Rank = 1,
            Url = "https://content.example/seo",
            Title = "SEO content",
            Description = "SEO description",
            HeadlineCount = 3,
            WordCount = 1200,
            CreatedAt = now
        });
        dbContext.CoOccurrenceWords.Add(new CoOccurrenceWordEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeywordId = keywordId,
            Word = "search intent",
            OccurrenceCountsJson = "{}",
            SiteCountsJson = "{}",
            CreatedAt = now
        });
        dbContext.ArticleBriefs.AddRange(
            new ArticleBriefEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Draft brief",
                TargetKeywordId = keywordId,
                CurrentVersion = 1,
                ContentJson = "{}",
                ReviewStatus = StatusValues.Pending,
                Status = "draft",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ArticleBriefEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Reviewed brief",
                TargetKeywordId = gapKeywordId,
                CurrentVersion = 1,
                ContentJson = "{}",
                ReviewStatus = "reviewed",
                Status = "active",
                CreatedAt = now,
                UpdatedAt = now
            });
        dbContext.Jobs.Add(new JobEntity
        {
            Id = rankJobId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            JobType = "RegisterRankCheckJob",
            Status = StatusValues.Succeeded,
            Progress = 100,
            RetryCount = 0,
            ResultResourceType = "rank_check_job",
            ResultResourceId = rankJobId,
            RequestedBy = "developer",
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = now
        });
        dbContext.RankResults.AddRange(
            new RankResultEntity
            {
                Id = Guid.NewGuid(),
                JobId = rankJobId,
                ProjectId = projectId,
                KeywordId = keywordId,
                Target = "own.example",
                Position = 2,
                RankedUrl = "https://own.example/seo",
                EstimatedTraffic = 10m,
                MetricsSnapshotJson = "{}",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                CheckedAt = now
            },
            new RankResultEntity
            {
                Id = Guid.NewGuid(),
                JobId = rankJobId,
                ProjectId = projectId,
                KeywordId = gapKeywordId,
                Target = "competitor.example",
                Position = 12,
                RankedUrl = "https://competitor.example/seo",
                EstimatedTraffic = 20m,
                MetricsSnapshotJson = "{}",
                ContractScopeKey = SeoIntelligenceSeedData.RakkoKeywordScopeKey,
                CheckedAt = now
            });
        dbContext.NotificationChannels.Add(new NotificationChannelEntity
        {
            Id = channelId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelType = "discord",
            Name = "Rank alert channel",
            WebhookSecretRef = "secret/rank-alert",
            EventTypesJson = """["rank_alert"]""",
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.Alerts.Add(new AlertEntity
        {
            Id = alertId,
            ProjectId = projectId,
            AlertType = "rank_drop",
            ConditionJson = """{"minDrop":3}""",
            NotificationChannelId = channelId,
            Status = StatusValues.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        var deliveryId = Guid.NewGuid();
        dbContext.AlertEvents.Add(new AlertEventEntity
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            ProjectId = projectId,
            JobId = rankJobId,
            KeywordId = keywordId,
            EventType = "rank_drop",
            PreviousValueJson = """{"position":2}""",
            CurrentValueJson = """{"position":8}""",
            EvidenceJson = "{}",
            NotificationDeliveryId = deliveryId,
            TriggeredAt = now
        });
        dbContext.NotificationDeliveries.Add(new NotificationDeliveryEntity
        {
            Id = deliveryId,
            WorkspaceId = SeoIntelligenceSeedData.DefaultWorkspaceId,
            ProjectId = projectId,
            ChannelId = channelId,
            JobId = rankJobId,
            ResourceType = "alert_event",
            ResourceId = alertId.ToString("D"),
            EventType = "rank_alert",
            PayloadHash = "rank-alert-payload",
            Status = StatusValues.Succeeded,
            RetryCount = 0,
            SentAt = now,
            DeliveredAt = now,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private static HttpClient CreateClient(SearchVolumeApiFactory factory)
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

    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "seo-intelligence-search-volume-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTempStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private sealed class SearchVolumeApiFactory : ServiceKeyApiFactory
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        public string StoragePath { get; } = CreateTempStoragePath();

        public SearchVolumeRakkoKeywordClient RakkoKeywordClient { get; } = new();

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
                    ["Storage:BasePath"] = StoragePath,
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
                services.RemoveAll<IRakkoKeywordClient>();
                services.AddSingleton<IRakkoKeywordClient>(RakkoKeywordClient);

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SeoIntelligenceDbContext>();
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            });
        }
    }

    private sealed class SearchVolumeRakkoKeywordClient : IRakkoKeywordClient
    {
        private readonly Dictionary<long, IReadOnlyList<string>> requests = new();
        private long nextRequestId = 7000001;

        public int ResultsCallCount { get; private set; }

        public int RegisterCallCount { get; private set; }

        /// <summary>
        /// requestIdを解釈できない契約違反レスポンス(invalid_response)を再現する。
        /// </summary>
        public bool FailRegistrationWithInvalidResponse { get; set; }

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeRegistration>> RegisterSearchVolumeAsync(
            RakkoKeywordClientContext context,
            RakkoSearchVolumeRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            RegisterCallCount++;
            if (FailRegistrationWithInvalidResponse)
            {
                return Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeRegistration>.Failure(
                    500,
                    ["Rakko Keyword API returned an invalid response."],
                    RakkoKeywordFailureKind.Fatal,
                    ExternalCall("/v1/search-volume", "invalid_response")));
            }

            var requestId = nextRequestId++;
            requests[requestId] = request.Keywords.ToArray();
            return Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeRegistration>.Success(
                new RakkoSearchVolumeRegistration(requestId),
                consumedCredit: 0m,
                statusCode: 200,
                ExternalCall("/v1/search-volume", null)));
        }

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeStatus>> GetSearchVolumeStatusAsync(
            RakkoKeywordClientContext context,
            long requestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeStatus>.Success(
                new RakkoSearchVolumeStatus(
                    IsCompleted: true,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["overall"] = "completed"
                    }),
                consumedCredit: 0m,
                statusCode: 200,
                ExternalCall($"/v1/search-volume/{requestId}/status", null)));

        public Task<RakkoKeywordCallResult<RakkoSearchVolumeResults>> GetSearchVolumeResultsAsync(
            RakkoKeywordClientContext context,
            long requestId,
            RakkoSearchVolumeResultsRequest request,
            CancellationToken cancellationToken = default)
        {
            ResultsCallCount++;
            var keywords = requests[requestId];
            var items = keywords
                .Select((keyword, index) => new RakkoSearchVolumeResultItem(
                    keyword,
                    "IntegrationFake",
                    new RakkoKeywordMetrics(
                        SeoDifficulty: 10 + (index % 50),
                        SearchVolume: 1000 + index + 1,
                        Cpc: 0.5m + (index % 10) / 10m,
                        Competition: 5 + (index % 20),
                        FirstSeenRange: "last_30_days"),
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["2026-04"] = 900 + index + 1,
                        ["2026-05"] = 1000 + index + 1
                    }))
                .Take(request.Limit)
                .ToArray();

            return Task.FromResult(RakkoKeywordCallResult<RakkoSearchVolumeResults>.Success(
                new RakkoSearchVolumeResults(items),
                consumedCredit: 5m,
                statusCode: 200,
                ExternalCall($"/v1/search-volume/{requestId}/results", null)));
        }

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetSuggestKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoSuggestKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRelatedKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoRelatedKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetOtherKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoOtherKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoQuestions>> GetQuestionsAsync(
            RakkoKeywordClientContext context,
            RakkoQuestionSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoKeywordCandidates>> GetRankingKeywordsAsync(
            RakkoKeywordClientContext context,
            RakkoRankingKeywordsRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoLocationCatalog>> ListLocationsAsync(
            RakkoKeywordClientContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RakkoKeywordCallResult<RakkoLanguageCatalog>> ListLanguagesAsync(
            RakkoKeywordClientContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static RakkoKeywordExternalCallRecord ExternalCall(string endpoint, string? errorCode)
            => new(
                Guid.NewGuid(),
                $"{endpoint}-request-hash",
                $"storage://local/{endpoint.Trim('/').Replace('/', '-')}-request.json.gz",
                errorCode is null ? $"{endpoint}-response-hash" : null,
                errorCode is null ? $"storage://local/{endpoint.Trim('/').Replace('/', '-')}-response.json.gz" : null,
                CacheHit: false,
                errorCode);
    }
}
