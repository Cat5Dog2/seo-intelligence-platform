using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace E2ETests;

internal sealed class BrowserSmokeApi(string apiUrl)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SmokeProject> CreateSmokeProjectAsync()
    {
        using var httpClient = CreateHttpClient();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        using var response = await httpClient.PostAsJsonAsync(
            "/api/projects",
            new
            {
                name = $"Browser smoke {stamp}",
                defaultLocation = "Japan",
                defaultLanguage = "Japanese",
                kpi = new { },
                memo = "Created by BrowserSmokeTests"
            },
            JsonOptions);

        var data = await ReadEnvelopeDataAsync(response);
        return new SmokeProject(
            data.GetProperty("projectId").GetString() ?? throw new InvalidOperationException("Project creation response did not include projectId."),
            data.GetProperty("name").GetString() ?? throw new InvalidOperationException("Project creation response did not include name."));
    }

    public async Task WaitForReportCompletedAsync(string projectId, string reportId)
    {
        using var httpClient = CreateHttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        string? lastStatus = null;
        string? lastFileUri = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await httpClient.GetAsync($"/api/projects/{projectId}/reports/{reportId}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }

            var data = await ReadEnvelopeDataAsync(response);
            lastStatus = data.GetProperty("status").GetString();
            lastFileUri = data.TryGetProperty("fileUri", out var fileUriElement)
                ? fileUriElement.GetString()
                : null;

            if (string.Equals(lastStatus, "completed", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(lastFileUri))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Report {reportId} did not complete. Last status={lastStatus ?? "<none>"}, fileUri={lastFileUri ?? "<none>"}.");
    }

    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(apiUrl, UriKind.Absolute)
        };

        // The API rejects every call that does not carry the service key the Web host uses.
        client.DefaultRequestHeaders.Add("X-Service-Key", ResolveServiceKey());
        return client;
    }

    private static string ResolveServiceKey()
    {
        DotEnvEnvironment.LoadRepositoryDotEnv();
        return Environment.GetEnvironmentVariable("E2E_API_SERVICE_KEY") is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("E2E_API_SERVICE_KEY is required when E2E_BROWSER_ENABLED=true.");
    }

    private static async Task<JsonElement> ReadEnvelopeDataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        if (!root.GetProperty("result").GetBoolean())
        {
            throw new InvalidOperationException("API response returned result=false.");
        }

        return root.GetProperty("data").Clone();
    }
}

internal sealed record SmokeProject(string ProjectId, string Name);
