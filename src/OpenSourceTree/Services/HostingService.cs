using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenSourceTree.Services;

public sealed record RemoteRepo(string FullName, string CloneUrl, string Description, bool IsPrivate);

/// <summary>
/// Lists repositories from hosting providers for the New tab's Remote view.
/// With a personal access token the account's own (incl. private) repositories are listed;
/// without one, only the user's public repositories.
/// </summary>
public static class HostingService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenSourceTree/0.1");
        return client;
    }

    public static Task<List<RemoteRepo>> ListAsync(HostingAccount account) =>
        account.Provider.Equals("GitLab", StringComparison.OrdinalIgnoreCase)
            ? ListGitLabAsync(account)
            : ListGitHubAsync(account);

    private static async Task<string> GetAsync(HttpRequestMessage request)
    {
        using var response = await Http.SendAsync(request).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"{(int)response.StatusCode} {response.ReasonPhrase} — check the username, token and server URL.");
        return body;
    }

    // ---------- GitHub ----------

    private static async Task<List<RemoteRepo>> ListGitHubAsync(HostingAccount account)
    {
        bool authed = !string.IsNullOrWhiteSpace(account.Token);
        var result = new List<RemoteRepo>();

        for (int page = 1; page <= 3; page++)
        {
            string url = authed
                ? $"https://api.github.com/user/repos?per_page=100&sort=updated&page={page}"
                : $"https://api.github.com/users/{Uri.EscapeDataString(account.Username)}/repos?per_page=100&sort=updated&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            if (authed)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token.Trim());

            using var doc = JsonDocument.Parse(await GetAsync(request).ConfigureAwait(false));
            int count = 0;
            foreach (var repo in doc.RootElement.EnumerateArray())
            {
                count++;
                result.Add(new RemoteRepo(
                    repo.GetProperty("full_name").GetString() ?? "",
                    repo.GetProperty("clone_url").GetString() ?? "",
                    repo.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                        ? d.GetString() ?? "" : "",
                    repo.TryGetProperty("private", out var p) && p.GetBoolean()));
            }
            if (count < 100)
                break;
        }

        return result;
    }

    // ---------- GitLab (gitlab.com or self-hosted) ----------

    private static async Task<List<RemoteRepo>> ListGitLabAsync(HostingAccount account)
    {
        string baseUrl = string.IsNullOrWhiteSpace(account.BaseUrl)
            ? "https://gitlab.com"
            : account.BaseUrl!.TrimEnd('/');
        bool authed = !string.IsNullOrWhiteSpace(account.Token);
        var result = new List<RemoteRepo>();

        for (int page = 1; page <= 3; page++)
        {
            string url = authed
                ? $"{baseUrl}/api/v4/projects?membership=true&per_page=100&order_by=last_activity_at&page={page}"
                : $"{baseUrl}/api/v4/users/{Uri.EscapeDataString(account.Username)}/projects?per_page=100&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (authed)
                request.Headers.Add("PRIVATE-TOKEN", account.Token.Trim());

            using var doc = JsonDocument.Parse(await GetAsync(request).ConfigureAwait(false));
            int count = 0;
            foreach (var repo in doc.RootElement.EnumerateArray())
            {
                count++;
                result.Add(new RemoteRepo(
                    repo.GetProperty("path_with_namespace").GetString() ?? "",
                    repo.GetProperty("http_url_to_repo").GetString() ?? "",
                    repo.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                        ? d.GetString() ?? "" : "",
                    repo.TryGetProperty("visibility", out var v) && v.GetString() != "public"));
            }
            if (count < 100)
                break;
        }

        return result;
    }
}
