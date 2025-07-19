using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.Text.Json;
using Tasked.Services;

namespace Tasked.Setup;

public class TokenUpdateService
{
    private readonly string _localConfigPath;

    public TokenUpdateService()
    {
        _localConfigPath = "appsettings.local.json";
    }

    /// <summary>
    /// Interactive token replacement wizard
    /// </summary>
    public async Task<bool> RunTokenUpdateAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel("[bold yellow]🔄 Token Update Wizard[/]")
                .Border(BoxBorder.Double)
                .BorderColor(Color.Yellow)
                .Padding(1, 0));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Update API Tokens[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("This wizard will help you replace expired or compromised API tokens.");
        AnsiConsole.MarkupLine("[dim]Your existing configuration will be preserved, only selected tokens will be updated.[/]");
        AnsiConsole.WriteLine();

        // Check if configuration exists
        if (!File.Exists(_localConfigPath))
        {
            AnsiConsole.MarkupLine("[red]❌ No configuration found. Run setup first:[/]");
            AnsiConsole.MarkupLine("[cyan]dotnet run --setup[/]");
            return false;
        }

        // Load current configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(_localConfigPath, optional: true)
            .Build();

        // First, show current token health
        await ShowCurrentTokenStatusAsync(config);

        // Select which token to update
        var tokenToUpdate = SelectTokenToUpdate(config);
        if (tokenToUpdate == TokenType.None)
        {
            AnsiConsole.MarkupLine("[yellow]Token update cancelled.[/]");
            return false;
        }

        // Update the selected token
        bool success = tokenToUpdate switch
        {
            TokenType.Jira => await UpdateJiraTokenAsync(),
            TokenType.BitBucket => await UpdateBitBucketTokenAsync(),
            _ => false
        };

        if (success)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✅ Token updated successfully![/]");
            AnsiConsole.MarkupLine("Testing new token...");

            // Test the updated token
            await TestUpdatedTokenAsync(tokenToUpdate);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]🎉 Token replacement completed![/]");
            AnsiConsole.MarkupLine("[dim]Run 'dotnet run --token-health' to verify all tokens are working.[/]");
        }

        return success;
    }

    /// <summary>
    /// Show current token status before updating
    /// </summary>
    private async Task ShowCurrentTokenStatusAsync(IConfiguration config)
    {
        AnsiConsole.MarkupLine("[bold]Current Token Status:[/]");
        AnsiConsole.WriteLine();

        try
        {
            var httpClient = new HttpClient();
            var tokenHealth = new TokenHealthService(config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<TokenHealthService>(),
                httpClient);

            var report = await tokenHealth.CheckAllTokensAsync();

            // Create a simple status table
            var table = new Table();
            table.AddColumn("Service");
            table.AddColumn("Status");

            var jiraStatus = GetSimpleStatus(report.JiraStatus);
            var repoStatus = GetSimpleStatus(report.RepositoryStatus);

            table.AddRow("Jira", jiraStatus);
            table.AddRow("Repository", repoStatus);

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            httpClient.Dispose();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not check current status: {ex.Message}[/]");
            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Let user select which token to update
    /// </summary>
    private TokenType SelectTokenToUpdate(IConfiguration config)
    {
        var choices = new List<string>();

        // Check what tokens are configured
        var jiraToken = config["Jira:ApiToken"];
        var repoAppPassword = config["RepositoryProviders:0:AppPassword"];
        var repoApiToken = config["RepositoryProviders:0:ApiToken"];
        var repoAccessToken = config["RepositoryProviders:0:AccessToken"]; // Legacy

        if (!string.IsNullOrEmpty(jiraToken) && jiraToken != "your-api-token")
        {
            choices.Add("Jira API Token");
        }

        var hasRepoCredential = (!string.IsNullOrEmpty(repoAppPassword) && repoAppPassword != "your-bitbucket-app-password") ||
                               (!string.IsNullOrEmpty(repoApiToken) && repoApiToken != "your-bitbucket-api-token") ||
                               (!string.IsNullOrEmpty(repoAccessToken) && repoAccessToken != "your-bitbucket-token");

        if (hasRepoCredential)
        {
            choices.Add("BitBucket Credentials");
        }

        if (!choices.Any())
        {
            AnsiConsole.MarkupLine("[red]❌ No tokens are currently configured.[/]");
            return TokenType.None;
        }

        choices.Add("Cancel - Don't update any tokens");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Which token would you like to update?[/]")
                .AddChoices(choices));

        return selection switch
        {
            "Jira API Token" => TokenType.Jira,
            "BitBucket Credentials" => TokenType.BitBucket,
            _ => TokenType.None
        };
    }

    /// <summary>
    /// Update Jira API token
    /// </summary>
    private async Task<bool> UpdateJiraTokenAsync()
    {
        AnsiConsole.Write(new Rule("[bold blue]🔑 Update Jira API Token[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Let's replace your Jira API token with a new one.");
        AnsiConsole.WriteLine();

        // Instructions for creating new token
        AnsiConsole.MarkupLine("[bold yellow]📝 Create a New API Token:[/]");
        AnsiConsole.MarkupLine("1. Open: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
        AnsiConsole.MarkupLine("2. Click [bold]'Create API token'[/]");
        AnsiConsole.MarkupLine("3. Label it: [bold]'Tasked Application (Updated)'[/]");
        AnsiConsole.MarkupLine("4. [bold red]⚠️  Copy the new token immediately![/]");
        AnsiConsole.MarkupLine("5. [bold]Optionally:[/] Delete your old token for security");
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Have you created the new API token and copied it?"))
        {
            AnsiConsole.MarkupLine("[yellow]Please create the new token first, then try again.[/]");
            return false;
        }

        // Get new token
        string newApiToken = AnsiConsole.Prompt(
            new TextPrompt<string>("Paste your new API token:")
                .Secret('*'));

        // Update configuration
        await UpdateConfigurationValueAsync("Jira:ApiToken", newApiToken);

        AnsiConsole.MarkupLine("[green]✅ Jira API token updated![/]");
        return true;
    }

    /// <summary>
    /// Update BitBucket App Password
    /// </summary>
    private async Task<bool> UpdateBitBucketTokenAsync()
    {
        // Determine credential type based on date
        var appPasswordEndDate = new DateTime(2025, 9, 9);
        var today = DateTime.UtcNow;
        bool useAppPassword = today < appPasswordEndDate;

        string credentialType = useAppPassword ? "App Password" : "API Token";
        string title = useAppPassword ? "🔑 Update BitBucket App Password" : "🔑 Update BitBucket API Token";

        AnsiConsole.Write(new Rule($"[bold blue]{title}[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"Let's replace your BitBucket {credentialType} with a new one.");
        AnsiConsole.WriteLine();

        if (useAppPassword)
        {
            // Instructions for creating new App Password
            AnsiConsole.MarkupLine("[bold yellow]📝 Create a New App Password:[/]");
            AnsiConsole.MarkupLine("1. Open: [link]https://bitbucket.org/account/settings/app-passwords/[/]");
            AnsiConsole.MarkupLine("2. Click [bold]'Create app password'[/]");
            AnsiConsole.MarkupLine("3. Name: [bold]'Tasked Application (Updated)'[/]");
            AnsiConsole.MarkupLine("4. [bold]Required Permissions:[/]");
            AnsiConsole.MarkupLine("   • Repositories: Read ✅");
            AnsiConsole.MarkupLine("   • Repositories: Write ✅");
            AnsiConsole.MarkupLine("   • Pull requests: Read ✅");
            AnsiConsole.MarkupLine("   • Pull requests: Write ✅");
            AnsiConsole.MarkupLine("5. [bold red]⚠️  Copy the new password immediately![/]");
            AnsiConsole.MarkupLine("6. [bold]Optionally:[/] Delete your old App Password for security");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Note: App Passwords will be deprecated on September 9, 2025 in favor of API Tokens.[/]");
        }
        else
        {
            // Instructions for creating new API Token
            AnsiConsole.MarkupLine("[bold yellow]📝 Create a New API Token:[/]");
            AnsiConsole.MarkupLine("1. Open: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
            AnsiConsole.MarkupLine("2. Click [bold]'Create API token'[/]");
            AnsiConsole.MarkupLine("3. Label: [bold]'Tasked Application (Updated)'[/]");
            AnsiConsole.MarkupLine("4. [bold red]⚠️  Copy the new token immediately![/]");
            AnsiConsole.MarkupLine("5. [bold]Optionally:[/] Delete your old token for security");
            AnsiConsole.WriteLine();
        }

        if (!AnsiConsole.Confirm($"Have you created the new {credentialType} and copied it?"))
        {
            AnsiConsole.MarkupLine($"[yellow]Please create the new {credentialType} first, then try again.[/]");
            return false;
        }

        // Get new credential
        string newCredential = AnsiConsole.Prompt(
            new TextPrompt<string>($"Paste your new {credentialType}:")
                .Secret('*'));

        // Update configuration - use the appropriate field
        string configKey = useAppPassword ? "RepositoryProviders:0:AppPassword" : "RepositoryProviders:0:ApiToken";
        await UpdateConfigurationValueAsync(configKey, newCredential);

        // Also get username if using App Password and not already set
        if (useAppPassword)
        {
            // Load current configuration to check if username exists
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile(_localConfigPath, optional: true)
                .Build();

            var existingUsername = config["RepositoryProviders:0:Username"];
            if (string.IsNullOrEmpty(existingUsername))
            {
                string username = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your BitBucket username:")
                        .DefaultValue("rockyassad"));
                await UpdateConfigurationValueAsync("RepositoryProviders:0:Username", username);
            }
        }

        AnsiConsole.MarkupLine($"[green]✅ BitBucket {credentialType} updated![/]");
        return true;
    }

    /// <summary>
    /// Update a specific configuration value
    /// </summary>
    private async Task UpdateConfigurationValueAsync(string keyPath, string newValue)
    {
        // Read current configuration
        var configJson = await File.ReadAllTextAsync(_localConfigPath);
        var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson);

        if (configData == null)
        {
            throw new InvalidOperationException("Could not parse configuration file");
        }

        // Navigate to the key and update it
        var keys = keyPath.Split(':');
        var current = configData;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];

            if (current.TryGetValue(key, out var value))
            {
                if (value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                        if (dict != null)
                        {
                            current[key] = dict;
                            current = dict;
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Array && i == keys.Length - 2)
                    {
                        // Handle array case (like RepositoryProviders:0:AccessToken)
                        var arrayItems = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(element.GetRawText());
                        var index = int.Parse(keys[i + 1]);
                        var finalKey = keys[i + 2];

                        if (arrayItems != null && index < arrayItems.Count)
                        {
                            arrayItems[index][finalKey] = newValue;
                            current[key] = arrayItems;
                        }
                        return;
                    }
                }
                else if (value is Dictionary<string, object> dict)
                {
                    current = dict;
                }
            }
        }

        // Set the final value
        current[keys.Last()] = newValue;

        // Save updated configuration
        var options = new JsonSerializerOptions { WriteIndented = true };
        var updatedJson = JsonSerializer.Serialize(configData, options);
        await File.WriteAllTextAsync(_localConfigPath, updatedJson);
    }

    /// <summary>
    /// Test the updated token
    /// </summary>
    private async Task TestUpdatedTokenAsync(TokenType tokenType)
    {
        try
        {
            // Reload configuration
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile(_localConfigPath, optional: true)
                .Build();

            var httpClient = new HttpClient();
            var tokenHealth = new TokenHealthService(config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<TokenHealthService>(),
                httpClient);

            var report = await tokenHealth.CheckAllTokensAsync();

            var status = tokenType switch
            {
                TokenType.Jira => report.JiraStatus,
                TokenType.BitBucket => report.RepositoryStatus,
                _ => TokenStatus.Error
            };

            if (status == TokenStatus.Valid)
            {
                AnsiConsole.MarkupLine("[green]✅ New token is working correctly![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Token test failed. Please verify the token and try again.[/]");
            }

            httpClient.Dispose();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not test new token: {ex.Message}[/]");
        }
    }

    private string GetSimpleStatus(TokenStatus status) => status switch
    {
        TokenStatus.Valid => "[green]✅ Valid[/]",
        TokenStatus.Invalid => "[red]❌ Invalid[/]",
        TokenStatus.ExpiringSoon => "[yellow]⚠️  Expiring Soon[/]",
        TokenStatus.InsufficientPermissions => "[orange3]🔒 Limited Access[/]",
        TokenStatus.AccountInactive => "[red]👤 Account Issue[/]",
        TokenStatus.NotConfigured => "[grey]➖ Not Set[/]",
        TokenStatus.Error => "[red]💥 Error[/]",
        _ => "[grey]❓ Unknown[/]"
    };
}

public enum TokenType
{
    None,
    Jira,
    BitBucket
}
