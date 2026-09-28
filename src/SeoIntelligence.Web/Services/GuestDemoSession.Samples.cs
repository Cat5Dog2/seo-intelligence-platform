using System.Globalization;
using System.Text.Json;
using SeoIntelligence.Application.Services;

namespace SeoIntelligence.Web.Services;

public sealed partial class GuestDemoSession
{
    private readonly Dictionary<Guid, DemoPortfolio> _portfolios = [];
    private readonly DateTime _sampleTime = DateTime.UtcNow.AddDays(-1);
    private sealed record DemoTopic(Guid Id, string Name, string Title, string Slug, KeywordCandidate[] Keywords);
    private sealed record DemoPortfolio(DemoTopic[] Topics, CompetitorResultRow[] Competitors,
        InfluxKeywordResultRow[] InfluxKeywords, InfluxPageResultRow[] InfluxPages, ContentAnalysisResultRow[] Analyses,
        TopicClusterSummary[] Clusters, ArticleBriefSummary[] Briefs, RankResultRow[] Ranks,
        RewriteTaskDetails[] Rewrites, CannibalizationCandidateDetails[] Cannibalization);
    public sealed record DemoSampleSize(int Themes, int Keywords, int Competitors, int Briefs);

    private static readonly (string Name, string Title, string Slug, string[] Words)[] SampleThemes =
    [
        ("SEOの基礎（デモ）", "SEO入門記事（デモ）", "seo", ["SEO 対策", "SEO とは", "SEO 始め方", "SEO 内部対策", "SEO 効果測定"]),
        ("キーワード選定（デモ）", "キーワード選定の実践ガイド（デモ）", "keywords", ["キーワード 選定", "キーワード 検索意図", "キーワード 調査", "ロングテール キーワード", "キーワード 競合分析"]),
        ("コンテンツ改善（デモ）", "記事リライトの進め方（デモ）", "rewrite", ["記事 リライト", "記事 見出し", "記事 内部リンク", "コンテンツ 分析", "記事 順位改善"])
    ];

    /// <summary>Sizes quoted by the login, walkthrough and sample report. Each theme seeds one competitor and one brief.</summary>
    public static DemoSampleSize SampleSize
        => new(SampleThemes.Length, SampleThemes.Sum(theme => theme.Words.Length), SampleThemes.Length, SampleThemes.Length);

    private static IReadOnlyDictionary<string, int> MonthlyVolumes(int baseline, int months)
        => Enumerable.Range(1, months).ToDictionary(
            offset => DateTime.UtcNow.AddMonths(-months + offset - 1).ToString("yyyyMM", CultureInfo.InvariantCulture),
            offset => baseline * (80 + offset * 2) / 100);

    private DemoPortfolio Portfolio(Guid projectId)
    {
        if (_portfolios.TryGetValue(projectId, out var existing)) return existing;
        var topics = SampleThemes.Select((theme, index) => new DemoTopic(Guid.NewGuid(), theme.Name, theme.Title, theme.Slug,
            theme.Words.Select((word, i) => new KeywordCandidate(word, "suggest", "+", 92 - index * 8 - i * 3,
                Guid.NewGuid(), Engine: "google", SearchVolume: 2400 - index * 500 - i * 150,
                SeoDifficulty: 25 + index * 5 + i * 3, Cpc: 0.4m + index * 0.1m, Competition: 0.25m + i * 0.05m)).ToArray())).ToArray();
        const string domain = "learn-seo.example";
        string Url(DemoTopic topic) => $"https://{domain}/{topic.Slug}";
        JsonElement Metrics(KeywordCandidate keyword) => JsonSerializer.SerializeToElement(new
            { searchVolume = keyword.SearchVolume, seoDifficulty = keyword.SeoDifficulty, dataSource = "Mock" });
        var competitors = topics.Select((topic, i) => new CompetitorResultRow(Guid.NewGuid(), $"guide-{i + 1}.example",
            35 + i * 12, 2400 + i * 800, 120 + i * 50, 120 + i * 20, 40 + i * 10, 80 + i * 10, 60 - i * 10, false, _sampleTime)).ToArray();
        var influxKeywords = topics.SelectMany((topic, i) => topic.Keywords.Take(3).Select((keyword, j) =>
            new InfluxKeywordResultRow(Guid.NewGuid(), keyword.KeywordId!.Value, competitors[i].Domain, keyword.Keyword,
                3 + i * 4 + j, $"https://{competitors[i].Domain}/{topic.Slug}", 250 - i * 40 - j * 10,
                Metrics(keyword), j != 0, j == 0 ? "owned" : "competitor_only", _sampleTime))).ToArray();
        var influxPages = topics.Select((topic, i) => new InfluxPageResultRow(Guid.NewGuid(), competitors[i].Domain,
            $"https://{competitors[i].Domain}/{topic.Slug}", topic.Title, 12 + i * 4, 650 - i * 100, 100 + i * 20,
            topic.Keywords[0].KeywordId, topic.Keywords[0].Keyword, _sampleTime)).ToArray();
        var analyses = topics.Select((topic, i) => new ContentAnalysisResultRow(topic.Keywords[0].KeywordId!.Value, topic.Keywords[0].Keyword,
            [new(Guid.NewGuid(), influxPages[i].PageUrl, competitors[i].Domain, topic.Title, "調査から改善まで進めるためのガイド。", 650, 100, topic.Keywords[0].KeywordId, topic.Keywords[0].Keyword, _sampleTime)],
            [new(Guid.NewGuid(), 1, influxPages[i].PageUrl, topic.Title, "検索意図・具体例・効果測定を含む構成", 3, 3200,
                [new(Guid.NewGuid(), 2, "読者の検索意図を理解する", 1), new(Guid.NewGuid(), 2, "手順を具体例で確認する", 2), new(Guid.NewGuid(), 2, "改善の効果を測る", 3)], _sampleTime)],
            new[] { "検索意図", "改善", "効果測定" }.Select((word, j) => new CoOccurrenceWordResultRow(Guid.NewGuid(), word,
                JsonSerializer.SerializeToElement(new { all = 24 - j * 4, headline = 5 - j, title = 2 }),
                JsonSerializer.SerializeToElement(new { all = 8 - j, headline = 4, title = 2 }),
                [new(Guid.NewGuid(), 1, influxPages[i].PageUrl, topic.Title, 8 - j, 2, 1)], _sampleTime)).ToArray(), _sampleTime)).ToArray();
        var briefs = topics.Select(topic => new ArticleBriefSummary(topic.Id, projectId, topic.Id, topic.Title,
            topic.Keywords[0].KeywordId, topic.Keywords[0].Keyword, 1, SampleBrief(topic), "pending", "active", _sampleTime, _sampleTime)).ToArray();
        var clusters = topics.Select((topic, i) => new TopicClusterSummary(topic.Id, projectId, topic.Name, null, null,
            topic.Keywords[0].KeywordId, topic.Keywords[0].Keyword, topic.Keywords[0].OpportunityScore!.Value, topic.Keywords.Length, "informational", 0,
            [new(topic.Id, topic.Title, topic.Keywords[0].KeywordId, topic.Keywords[0].Keyword, "informational", topic.Keywords[0].OpportunityScore!.Value, "active")],
            [new(topic.Id, topic.Name, topics[(i + 1) % topics.Length].Id, topics[(i + 1) % topics.Length].Name, "基礎の理解から具体的な実践へ読者を案内する")], _sampleTime, _sampleTime)).ToArray();
        var ranks = topics.SelectMany((topic, i) => topic.Keywords.Select((keyword, j) =>
            new RankResultRow(Guid.NewGuid(), topic.Id, keyword.KeywordId!.Value, keyword.Keyword, domain, 2 + i * 9 + j,
                5 + i * 9 + j, 3, Url(topic), 250 - i * 50 - j * 10, Metrics(keyword), "guest-mock", _sampleTime))).ToArray();
        var rewrites = topics.Select((topic, i) => new RewriteTaskDetails(topic.Id, projectId, Url(topic), 80 - i * 15,
            JsonSerializer.SerializeToElement(new { summary = i == 0 ? "検索意図に合わせて見出しを改善" : i == 1 ? "具体例を追加し、関連する記事への内部リンクを整える" : "古い手順を更新し、効果測定の方法を追記する", expectedImpact = "読者の疑問を解消し、次の行動につなげる" }),
            "active", "guest", "デモの提案です。", _sampleTime, _sampleTime)).ToArray();
        CannibalizationCandidateDetails[] cannibalization = [new(Guid.NewGuid(), projectId, topics[0].Keywords[0].KeywordId!.Value,
            topics[0].Keywords[0].Keyword, Url(topics[0]), JsonSerializer.SerializeToElement(new[] { $"https://{domain}/seo-basics" }), 72,
            JsonSerializer.SerializeToElement(new { summary = "2つの記事が同じ検索意図を扱い、見出しが重複しています。" }),
            JsonSerializer.SerializeToElement(new { summary = "入門記事に内容を集約し、関連記事の役割とリンク先を整理する。" }), "active", _sampleTime)];
        var portfolio = new DemoPortfolio(topics, competitors, influxKeywords, influxPages, analyses, clusters, briefs, ranks, rewrites, cannibalization);
        _portfolios.Add(projectId, portfolio);
        return portfolio;
    }

    private void SeedPortfolio(ProjectDetails project)
    {
        var portfolio = Portfolio(project.ProjectId);
        var site = new SiteDetails(Guid.NewGuid(), project.ProjectId, "learn-seo.example", "https://learn-seo.example",
            "own", "架空のSEO学習メディア。数値はすべて操作体験用のサンプルです。", "active", _sampleTime, _sampleTime, null);
        _sites.Add(site.SiteId, site);
        foreach (var topic in portfolio.Topics)
        {
            var discovery = AddJob(project.ProjectId, "KeywordDiscoveryJob");
            _jobs[discovery.JobId] = discovery with { CreatedAt = _sampleTime, UpdatedAt = _sampleTime, CompletedAt = _sampleTime };
            _discoveries.Add(discovery.JobId, new KeywordDiscoveryResult(topic.Keywords, SeedKeyword: topic.Keywords[0].Keyword,
                Location: project.DefaultLocation, Language: project.DefaultLanguage, Sources: ["suggest"], JobId: discovery.JobId,
                SourceStatuses: [new("suggest", "succeeded", topic.Keywords.Length)]));
            var volume = AddJob(project.ProjectId, "RegisterSearchVolumeJob");
            _jobs[volume.JobId] = volume with { CreatedAt = _sampleTime.AddMinutes(1), UpdatedAt = _sampleTime.AddMinutes(1), CompletedAt = _sampleTime.AddMinutes(1) };
            _volumes.Add(volume.JobId, topic.Keywords.Select(keyword => new SearchVolumeResultRow(keyword.Keyword,
                (int?)keyword.SearchVolume, keyword.SeoDifficulty, keyword.Cpc, keyword.Competition,
                MonthlyVolumes((int)keyword.SearchVolume!.Value, 12), DataSource: "Mock", KeywordId: keyword.KeywordId)).ToArray());
        }
    }

    private DashboardSnapshot PortfolioDashboard(Guid projectId, KeywordDiscoveryResult[] discoveries, IReadOnlyList<SearchVolumeResultRow>[] volumes)
    {
        var sample = Portfolio(projectId);
        var scores = discoveries.SelectMany(item => item.Candidates).Where(item => item.KeywordId.HasValue && item.OpportunityScore.HasValue)
            .OrderByDescending(item => item.OpportunityScore).ToArray();
        var project = _projects[projectId];
        return new(discoveries.Sum(item => item.Candidates.Count), 0, 0, 0, discoveries.Length, volumes.Length, volumes.Sum(item => item.Count),
            scores.Length, scores.Take(5).Select(item => new DashboardOpportunityScoreRow(item.KeywordId!.Value, item.Keyword,
                item.OpportunityScore!.Value, project.DefaultLocation, project.DefaultLanguage, _sampleTime)).ToArray(),
            CompetitorSummary: new(sample.Competitors.Length, 0, sample.Competitors.Average(row => row.DuplicateRate),
                sample.Competitors.Sum(row => row.EstimatedTraffic), sample.Competitors.Sum(row => row.TrafficValue)),
            InfluxSummary: new(sample.InfluxKeywords.Length, sample.InfluxKeywords.Count(row => row.IsGap), sample.InfluxPages.Length,
                sample.InfluxPages.Sum(row => row.EstimatedTraffic), sample.InfluxPages.Sum(row => row.TrafficValue)),
            ContentAnalysisSummary: new(sample.Analyses.Length, sample.Analyses.Sum(row => row.ContentResults.Count),
                sample.Analyses.Sum(row => row.HeadlinePages.Count), sample.Analyses.Sum(row => row.CoOccurrences.Count)),
            BriefSummary: new(sample.Briefs.Length, 0, sample.Briefs.Length, 0),
            RankSummary: new(0, sample.Ranks.Length, Distribution(sample.Ranks)),
            RewriteSummary: new(sample.Rewrites.Length, sample.Rewrites.Length, sample.Rewrites.Max(row => row.PriorityScore)),
            CannibalizationSummary: new(sample.Cannibalization.Length, sample.Cannibalization.Length, sample.Cannibalization.Max(row => row.SeverityScore)));
    }

    private static RankDistribution Distribution(IEnumerable<RankResultRow> rows)
        => new(rows.Count(row => row.Position is >= 1 and <= 3), rows.Count(row => row.Position is >= 4 and <= 10),
            rows.Count(row => row.Position is >= 11 and <= 20), rows.Count(row => row.Position is >= 21 and <= 50),
            rows.Count(row => row.Position is >= 51 and <= 100), rows.Count(row => row.Position is < 1 or > 100));

    private ApiClientResult<T> ReadSample<T>(string route, Guid projectId, Dictionary<string, string> query)
    {
        var sample = Portfolio(projectId);
        switch (route)
        {
            case "competitors": return Page<T, CompetitorResultRow>(sample.Competitors, query, row => row.Domain, row => row.DuplicateRate);
            case "influx-keywords": return Page<T, InfluxKeywordResultRow>(sample.InfluxKeywords.Where(row =>
                MatchesText(row.Target, query, "target") && MatchesRange(row.Rank, query, "minRank", "maxRank")), query, row => row.Keyword, row => row.Rank,
                row => query.GetValueOrDefault("sortBy") switch { "keyword" => row.Keyword, "estimatedTraffic" => row.EstimatedTraffic, "target" => row.Target, "createdAt" => row.CreatedAt, _ => row.Rank });
            case "influx-pages": return Page<T, InfluxPageResultRow>(sample.InfluxPages.Where(row => MatchesText(row.Target, query, "target")), query, row => row.Title, row => row.EstimatedTraffic,
                row => query.GetValueOrDefault("sortBy") switch { "keywordCount" => row.KeywordCount, "trafficValue" => row.TrafficValue, "title" => row.Title, _ => row.EstimatedTraffic });
            case "content-analyses": return Page<T, ContentAnalysisResultRow>(sample.Analyses, query, row => row.Keyword, row => row.LastAnalyzedAt);
            case "rank-results":
                var filteredRanks = sample.Ranks.Where(row => MatchesText(row.Keyword, query, "q") && MatchesText(row.Target, query, "target")
                    && MatchesRange(row.Position, query, "minPosition", "maxPosition")
                    && (!query.TryGetValue("keywordId", out var keywordId) || row.KeywordId.ToString() == keywordId)
                    && (!query.TryGetValue("jobId", out var jobId) || row.JobId.ToString() == jobId));
                IComparable RankSort(RankResultRow row) => query.GetValueOrDefault("sortBy") switch
                {
                    "position" => row.Position, "keyword" => row.Keyword, "estimatedTraffic" => row.EstimatedTraffic,
                    "positionDelta" => row.PositionDelta ?? 0, _ => row.CheckedAt
                };
                var ranks = (query.GetValueOrDefault("orderBy", "desc") == "desc"
                    ? filteredRanks.OrderByDescending(RankSort) : filteredRanks.OrderBy(RankSort)).ToArray();
                if (!int.TryParse(query.GetValueOrDefault("page", "1"), out var page) || page < 1
                    || !int.TryParse(query.GetValueOrDefault("pageSize", "50"), out var size) || size is < 1 or > 200) return Invalid<T>("ページと表示件数を確認してください。");
                var skip = (long)(page - 1) * size;
                return Ok<T>(new RankResultList(skip > int.MaxValue ? [] : ranks.Skip((int)skip).Take(size).ToArray(),
                    Distribution(ranks), page, size, ranks.Length, (int)Math.Ceiling((double)ranks.Length / size)));
            case "alerts": return Ok<T>(Array.Empty<RankAlertDetails>());
            case "alert-events": return Ok<T>(Array.Empty<RankAlertEventDetails>());
            case "clusters": return Page<T, TopicClusterSummary>(sample.Clusters.Where(row =>
                MatchesText(row.IntentLabel ?? "", query, "intentLabel") && (!query.TryGetValue("parentId", out var parentId) || row.ParentId?.ToString() == parentId)),
                query, row => row.Name, row => row.Score, row => query.GetValueOrDefault("sortBy") switch
                { "name" => row.Name, "representativeKeyword" => row.RepresentativeKeyword ?? "", "keywordCount" => row.KeywordCount, "childCount" => row.ChildCount, "updatedAt" => row.UpdatedAt, _ => row.Score });
            case "briefs":
                var review = query.GetValueOrDefault("reviewStatus");
                return Page<T, ArticleBriefSummary>(sample.Briefs.Where(row => string.IsNullOrWhiteSpace(review) || row.ReviewStatus == review), query, row => row.Title, row => row.UpdatedAt);
            case "rewrite/tasks": return Page<T, RewriteTaskDetails>(sample.Rewrites.Where(row => MatchesStatus(row.Status, query)), query, row => row.TargetUrl, row => row.PriorityScore);
            case "cannibalization/candidates": return Page<T, CannibalizationCandidateDetails>(sample.Cannibalization.Where(row => MatchesStatus(row.Status, query)), query, row => row.Keyword, row => row.SeverityScore);
            case "reports": return Ok<T>(Array.Empty<ReportDetails>());
            case "connectors": return Ok<T>(Array.Empty<ConnectorSettingsDetails>());
        }
        foreach (var topic in sample.Topics)
        {
            var cluster = sample.Clusters.Single(row => row.ClusterId == topic.Id);
            var brief = sample.Briefs.Single(row => row.BriefId == topic.Id);
            if (route == $"clusters/{topic.Id}")
                return Ok<T>(new TopicClusterDetails(topic.Id, projectId, topic.Name, null, null, topic.Keywords[0].KeywordId,
                    topic.Keywords[0].Keyword, cluster.Score, topic.Keywords.Length, "informational",
                    topic.Keywords.Select((keyword, i) => new TopicClusterKeywordRow(keyword.KeywordId!.Value, keyword.Keyword,
                        i == 0 ? "representative" : "related", keyword.OpportunityScore!.Value, "informational", new(0.9m, 0.6m, 2, ["suggest"]))).ToArray(),
                    [], cluster.ArticleCandidates, cluster.InternalLinkCandidates, _sampleTime, _sampleTime));
            if (route == $"briefs/{topic.Id}")
                return Ok<T>(new ArticleBriefDetails(topic.Id, projectId, topic.Id, brief.Title, brief.TargetKeywordId, brief.TargetKeyword,
                    brief.CurrentVersion, brief.Content, brief.ReviewStatus, brief.Status, brief.CreatedAt, brief.UpdatedAt));
            if (route == $"briefs/{topic.Id}/versions")
                return Ok<T>(new ArticleBriefVersionDetails[] { new(topic.Id, 1, "guest-mock", null, brief.Content, "guest", "pending", "初期デモ", _sampleTime) });
            if (route == $"rewrite/tasks/{topic.Id}") return Ok<T>(sample.Rewrites.Single(row => row.TaskId == topic.Id));
        }
        return Missing<T>();
    }

    private static bool MatchesText(string value, Dictionary<string, string> query, string key)
        => !query.TryGetValue(key, out var filter) || value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesRange(int value, Dictionary<string, string> query, string minKey, string maxKey)
        => (!query.TryGetValue(minKey, out var min) || int.TryParse(min, out var lower) && value >= lower)
            && (!query.TryGetValue(maxKey, out var max) || int.TryParse(max, out var upper) && value <= upper);

    private static JsonElement SampleBrief(DemoTopic topic) => JsonSerializer.SerializeToElement(new
    {
        summary = $"{topic.Keywords[0].Keyword}を調べている読者に、基礎知識と実践手順を届ける記事です。",
        targetKeyword = topic.Keywords[0].Keyword,
        searchIntent = "informational",
        headings = new[] { $"{topic.Keywords[0].Keyword}の基本", "調査結果から優先順位を決める", "具体例で学ぶ実践手順", "改善の効果を測る" },
        dataSource = "Mock"
    });
}
