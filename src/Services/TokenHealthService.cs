using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Tasked.Services;

public class TokenHealthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenHealthService> _logger;
    private readonly HttpClient _httpClient;

    public TokenHealthService(IConfiguration configuration, ILogger<TokenHealthService> logger, HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Checks the health of all configured API tokens
    /// </summary>
    public async Task<TokenHealthReport> CheckAllTokensAsync()
    {
        var report = new TokenHealthReport();

        // Check Jira token
        await CheckJiraTokenAsync(report);

        // Check repository tokens
        await CheckRepositoryTokensAsync(report);

        return report;
    }

    /// <summary>
    /// Checks Jira API token validity
    /// </summary>
    private async Task CheckJiraTokenAsync(TokenHealthReport report)
    {
        try
        {
            var jiraBaseUrl = _configuration["Jira:BaseUrl"];
            var username = _configuration["Jira:Username"];
            var apiToken = _configuration["Jira:ApiToken"];

            if (string.IsNullOrEmpty(jiraBaseUrl) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiToken))
            {
                report.JiraStatus = TokenStatus.NotConfigured;
                return;
            }

            // Test API call to validate token
            var request = new HttpRequestMessage(HttpMethod.Get, $"{jiraBaseUrl}/rest/api/3/myself");
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(content);

                report.JiraStatus = TokenStatus.Valid;
                report.JiraUsername = userInfo.GetProperty("displayName").GetString();
                report.JiraLastCheck = DateTime.Now;

                // Jira API tokens don't expire, but we can check if the account is active
                if (userInfo.TryGetProperty("active", out var activeElement) && !activeElement.GetBoolean())
                {
                    report.JiraStatus = TokenStatus.AccountInactive;
                    report.JiraIssues.Add("User account appears to be inactive");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                report.JiraStatus = TokenStatus.Invalid;
                report.JiraIssues.Add("Authentication failed - API Token may be invalid or revoked");
            }
            else
            {
                report.JiraStatus = TokenStatus.Error;
                report.JiraIssues.Add($"API call failed with status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            report.JiraStatus = TokenStatus.Error;
            report.JiraIssues.Add($"Connection error: {ex.Message}");
            _logger.LogError(ex, "Error checking Jira token health");
        }
    }

    /// <summary>
    /// Checks repository provider tokens
    /// </summary>
    private async Task CheckRepositoryTokensAsync(TokenHealthReport report)
    {
        var repositoryProviders = _configuration.GetSection("RepositoryProviders").Get<List<Dictionary<string, object>>>();

        if (repositoryProviders == null || !repositoryProviders.Any())
        {
            report.RepositoryStatus = TokenStatus.NotConfigured;
            return;
        }

        foreach (var provider in repositoryProviders)
        {
            if (!provider.TryGetValue("Name", out var nameObj) || nameObj?.ToString() != "BitBucket")
                continue;

            await CheckBitBucketTokenAsync(report, provider);
        }
    }

    /// <summary>
    /// Checks BitBucket API Token validity
    /// </summary>
    private async Task CheckBitBucketTokenAsync(TokenHealthReport report, Dictionary<string, object> provider)
    {
        try
        {
            var appPasswordEndDate = new DateTime(2025, 9, 9);
            var today = DateTime.UtcNow;
            bool useAppPassword = today < appPasswordEndDate;

            string credentialKey = useAppPassword ? "AppPassword" : "ApiToken";
            string credentialType = useAppPassword ? "App Password" : "API Token";

            // Check for the preferred credential type first, then fallback to the other
            object? tokenObj = null;
            if (provider.TryGetValue(credentialKey, out tokenObj))
            {
                // Found the preferred credential type
            }
            else if (provider.TryGetValue(useAppPassword ? "ApiToken" : "AppPassword", out tokenObj))
            {
                // Found the fallback credential type, update the type accordingly
                credentialKey = useAppPassword ? "ApiToken" : "AppPassword";
                credentialType = useAppPassword ? "API Token" : "App Password";
            }

            if (tokenObj == null || !provider.TryGetValue("WorkspaceOrOrganization", out var workspaceObj))
            {
                report.RepositoryStatus = TokenStatus.NotConfigured;
                return;
            }

            var token = tokenObj?.ToString();
            var workspace = workspaceObj?.ToString();

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(workspace))
            {
                report.RepositoryStatus = TokenStatus.NotConfigured;
                return;
            }

            // For BitBucket App Passwords, we need the actual username, not workspace
            // For API Tokens, we can use workspace or username
            string authUsername = workspace; // Default to workspace for API tokens

            if (useAppPassword)
            {
                // For App Passwords, try to get the BitBucket username from config
                if (provider.TryGetValue("Username", out var usernameObj) && !string.IsNullOrEmpty(usernameObj?.ToString()))
                {
                    authUsername = usernameObj.ToString()!;
                }
                else
                {
                    // Fallback: try to derive from Jira username
                    var jiraUsername = _configuration["Jira:Username"];
                    if (!string.IsNullOrEmpty(jiraUsername))
                    {
                        // Extract username part from email if it's an email
                        authUsername = jiraUsername.Contains("@") ? jiraUsername.Split("@")[0] : jiraUsername;
                    }
                    else
                    {
                        authUsername = workspace; // Last resort fallback
                    }
                }
            }

            // Test API call to validate credential
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.bitbucket.org/2.0/workspaces/{workspace}");
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authUsername}:{token}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var workspaceInfo = JsonSerializer.Deserialize<JsonElement>(content);

                report.RepositoryStatus = TokenStatus.Valid;
                report.RepositoryProvider = "BitBucket";
                report.RepositoryWorkspace = workspaceInfo.GetProperty("name").GetString();
                report.RepositoryLastCheck = DateTime.Now;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                report.RepositoryStatus = TokenStatus.Invalid;
                report.RepositoryIssues.Add($"Authentication failed - {credentialType} may be invalid or revoked");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                report.RepositoryStatus = TokenStatus.InsufficientPermissions;
                report.RepositoryIssues.Add($"{credentialType} lacks required permissions");
            }
            else
            {
                report.RepositoryStatus = TokenStatus.Error;
                report.RepositoryIssues.Add($"API call failed with status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            report.RepositoryStatus = TokenStatus.Error;
            report.RepositoryIssues.Add($"Connection error: {ex.Message}");
            _logger.LogError(ex, "Error checking BitBucket token health");
        }
    }

    /// <summary>
    /// Displays a user-friendly token health report
    /// </summary>
    public void DisplayHealthReport(TokenHealthReport report)
    {
        AnsiConsole.Write(new Rule("[bold blue]🔐 Token Health Report[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        // Create a table for the health report
        var table = new Table();
        table.AddColumn("Service");
        table.AddColumn("Status");
        table.AddColumn("Details");
        table.AddColumn("Action Required");

        // Jira row
        var jiraStatus = GetStatusMarkup(report.JiraStatus);
        var jiraDetails = report.JiraStatus == TokenStatus.Valid
            ? $"User: {report.JiraUsername}\nLast checked: {report.JiraLastCheck:yyyy-MM-dd HH:mm}"
            : string.Join("\n", report.JiraIssues);
        var jiraAction = GetActionRequired(report.JiraStatus, "Jira");

        table.AddRow("Jira", jiraStatus, jiraDetails, jiraAction);

        // Repository rows (support multiple providers)
        var repositoryProviders = _configuration.GetSection("RepositoryProviders").Get<List<Dictionary<string, object>>>();
        if (repositoryProviders != null && repositoryProviders.Any())
        {
            foreach (var provider in repositoryProviders)
            {
                var name = provider.TryGetValue("Name", out var nameObj) ? nameObj?.ToString() ?? "Repository" : "Repository";
                var workspace = provider.TryGetValue("WorkspaceOrOrganization", out var wsObj) ? wsObj?.ToString() ?? "" : "";
                var status = GetStatusMarkup(report.RepositoryStatus);
                var details = report.RepositoryStatus == TokenStatus.Valid
                    ? $"Provider: {name}\nWorkspace: {workspace}\nLast checked: {report.RepositoryLastCheck:yyyy-MM-dd HH:mm}"
                    : string.Join("\n", report.RepositoryIssues);
                var action = GetActionRequired(report.RepositoryStatus, name);
                table.AddRow(name, status, details, action);
            }
        }
        else
        {
            var status = GetStatusMarkup(report.RepositoryStatus);
            var details = string.Join("\n", report.RepositoryIssues);
            var action = GetActionRequired(report.RepositoryStatus, "Repository");
            table.AddRow("Repository", status, details, action);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Show recommendations
        ShowRecommendations(report);
    }

    /// <summary>
    /// Shows recommendations based on token health
    /// </summary>
    private void ShowRecommendations(TokenHealthReport report)
    {
        var hasIssues = report.JiraStatus != TokenStatus.Valid || report.RepositoryStatus != TokenStatus.Valid;

        if (!hasIssues)
        {
            AnsiConsole.MarkupLine("[green]✅ All tokens are healthy![/]");
            AnsiConsole.MarkupLine("[dim]💡 Tip: Run 'dotnet run --token-health' periodically to monitor token status[/]");
            return;
        }

        AnsiConsole.MarkupLine("[yellow]⚠️  Token Issues Detected[/]");
        AnsiConsole.WriteLine();

        if (report.JiraStatus != TokenStatus.Valid)
        {
            AnsiConsole.MarkupLine("[bold]Jira Token Issues:[/]");
            AnsiConsole.MarkupLine("• Run [cyan]dotnet run --setup[/] to reconfigure Jira");
            AnsiConsole.MarkupLine("• Or visit: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
            AnsiConsole.WriteLine();
        }

        if (report.RepositoryStatus != TokenStatus.Valid)
        {
            var repositoryProviders = _configuration.GetSection("RepositoryProviders").Get<List<Dictionary<string, object>>>();
            if (repositoryProviders != null && repositoryProviders.Any())
            {
                foreach (var provider in repositoryProviders)
                {
                    var name = provider.TryGetValue("Name", out var nameObj) ? nameObj?.ToString() ?? "Repository" : "Repository";

                    // BitBucket authentication transition dates
                    var appPasswordEndDate = new DateTime(2025, 9, 9);
                    var today = DateTime.UtcNow;
                    bool useAppPassword = today < appPasswordEndDate;

                    if (name.Equals("BitBucket", StringComparison.OrdinalIgnoreCase))
                    {
                        AnsiConsole.MarkupLine($"[bold]{name} Token Issues:[/]");
                        AnsiConsole.MarkupLine("• Run [cyan]dotnet run --setup[/] to reconfigure repository access");

                        if (useAppPassword)
                        {
                            AnsiConsole.MarkupLine("• Or visit: [link]https://bitbucket.org/account/settings/app-passwords/[/] (App Passwords)");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("• Or visit: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/] (API Tokens)");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[bold]{name} Token Issues:[/]");
                        AnsiConsole.MarkupLine("• Run [cyan]dotnet run --setup[/] to reconfigure repository access");
                        AnsiConsole.MarkupLine("• Or visit: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
                    }
                    AnsiConsole.WriteLine();
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[bold]Repository Token Issues:[/]");
                AnsiConsole.MarkupLine("• Run [cyan]dotnet run --setup[/] to reconfigure repository access");
                AnsiConsole.MarkupLine("• Or visit: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.MarkupLine("[dim]🔄 Tokens should be recreated if they've been compromised or revoked[/]");
    }

    private string GetStatusMarkup(TokenStatus status) => status switch
    {
        TokenStatus.Valid => "[green]✅ Valid[/]",
        TokenStatus.Invalid => "[red]❌ Invalid[/]",
        TokenStatus.ExpiringSoon => "[yellow]⚠️  Expiring Soon[/]",
        TokenStatus.InsufficientPermissions => "[orange3]🔒 Insufficient Permissions[/]",
        TokenStatus.AccountInactive => "[red]👤 Account Inactive[/]",
        TokenStatus.NotConfigured => "[grey]➖ Not Configured[/]",
        TokenStatus.Error => "[red]💥 Error[/]",
        _ => "[grey]❓ Unknown[/]"
    };

    private string GetActionRequired(TokenStatus status, string service) => status switch
    {
        TokenStatus.Valid => "None",
        TokenStatus.Invalid => "Recreate token",
        TokenStatus.ExpiringSoon => "Renew token soon",
        TokenStatus.InsufficientPermissions => "Update permissions",
        TokenStatus.AccountInactive => "Activate account",
        TokenStatus.NotConfigured => "Configure token",
        TokenStatus.Error => "Check connection",
        _ => "Review status"
    };
}

public class TokenHealthReport
{
    public TokenStatus JiraStatus { get; set; } = TokenStatus.NotConfigured;
    public string? JiraUsername { get; set; }
    public DateTime? JiraLastCheck { get; set; }
    public List<string> JiraIssues { get; set; } = new();

    public TokenStatus RepositoryStatus { get; set; } = TokenStatus.NotConfigured;
    public string? RepositoryProvider { get; set; }
    public string? RepositoryWorkspace { get; set; }
    public DateTime? RepositoryLastCheck { get; set; }
    public List<string> RepositoryIssues { get; set; } = new();
}

public enum TokenStatus
{
    Valid,
    Invalid,
    ExpiringSoon,
    InsufficientPermissions,
    AccountInactive,
    NotConfigured,
    Error
}
