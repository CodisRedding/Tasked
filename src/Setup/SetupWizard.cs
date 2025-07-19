using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.Text.Json;
using Tasked.Configuration;
using Tasked.Services;

namespace Tasked.Setup;

public class SetupWizard
{
    private readonly string _configPath;
    private readonly string _localConfigPath;

    public SetupWizard()
    {
        _configPath = "appsettings.json";
        _localConfigPath = "appsettings.local.json";
    }

    public async Task<bool> RunSetupAsync()
    {
        AnsiConsole.Write(
            new FigletText("Tasked Setup")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Welcome to Tasked - Automated Task Management System![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("This wizard will guide you through setting up your Jira and repository connections.");
        AnsiConsole.MarkupLine("[dim]All sensitive data will be stored securely in appsettings.local.json (excluded from version control).[/]");
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Ready to begin setup?"))
        {
            AnsiConsole.MarkupLine("[yellow]Setup cancelled. You can run setup again later with: dotnet run --setup[/]");
            return false;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(_configPath, optional: false)
            .AddJsonFile(_localConfigPath, optional: true)
            .Build();

        var setupConfig = new Dictionary<string, object>();

        // Step 1: Jira Setup
        if (!await SetupJiraAsync(setupConfig))
            return false;

        // Step 2: BitBucket Setup
        if (!await SetupRepositoryAsync(setupConfig))
            return false;

        // Step 3: Workflow Settings
        await SetupWorkflowAsync(setupConfig);

        // Step 4: Save Configuration
        await SaveConfigurationAsync(setupConfig);

        // Step 5: Test Connection
        await TestConnectionsAsync();

        AnsiConsole.MarkupLine("[bold green]🎉 Setup completed successfully![/]");
        AnsiConsole.MarkupLine("You can now run the application with: [bold]dotnet run[/]");

        return true;
    }

    private async Task<bool> SetupJiraAsync(Dictionary<string, object> config)
    {
        AnsiConsole.Write(new Rule("[bold blue]📋 Jira Configuration[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        // Check for existing Jira configuration first
        var existingConfig = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(_configPath, optional: false)
            .AddJsonFile(_localConfigPath, optional: true)
            .Build();

        var existingBaseUrl = existingConfig["Jira:BaseUrl"];
        var existingUsername = existingConfig["Jira:Username"];
        var existingApiToken = existingConfig["Jira:ApiToken"];
        var existingJqlQuery = existingConfig["Jira:JqlQuery"];

        bool hasExistingConfig = !string.IsNullOrEmpty(existingApiToken) &&
                                existingApiToken != "your-api-token" &&
                                existingApiToken != "REPLACE IN appsettings.local.json" &&
                                !string.IsNullOrEmpty(existingBaseUrl) &&
                                existingBaseUrl != "https://your-domain.atlassian.net";

        if (hasExistingConfig)
        {
            AnsiConsole.MarkupLine("[green]📋 Existing Jira configuration detected![/]");
            AnsiConsole.MarkupLine($"[dim]Domain: {existingBaseUrl}[/]");
            AnsiConsole.MarkupLine($"[dim]Username: {existingUsername}[/]");
            AnsiConsole.MarkupLine($"[dim]JQL Query: {existingJqlQuery ?? "assignee = currentUser() AND status IN (\"To Do\", \"In Progress\", \"Ready for Development\")"}[/]");
            AnsiConsole.MarkupLine($"[dim]API Token: ****...{existingApiToken?.Substring(Math.Max(0, existingApiToken.Length - 8))}[/]");
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]What would you like to do?[/]")
                    .AddChoices(
                        "Keep existing configuration",
                        "Edit existing values (keep what you want)",
                        "Start over with fresh configuration"));

            if (choice == "Keep existing configuration")
            {
                // Preserve existing configuration exactly as-is
                config["Jira"] = new Dictionary<string, object>
                {
                    ["BaseUrl"] = existingBaseUrl!,
                    ["Username"] = existingUsername!,
                    ["ApiToken"] = existingApiToken!,
                    ["JqlQuery"] = existingJqlQuery ?? "assignee = currentUser() AND status IN (\"To Do\", \"In Progress\", \"Ready for Development\")",
                    ["SyncIntervalMinutes"] = existingConfig.GetValue<int>("Jira:SyncIntervalMinutes", 15)
                };

                await SaveConfigurationSectionAsync("Jira", config["Jira"]);
                AnsiConsole.MarkupLine("[green]✅ Keeping existing Jira configuration![/]");
                return true;
            }
            else if (choice == "Edit existing values (keep what you want)")
            {
                AnsiConsole.MarkupLine("[yellow]You can update individual fields (press Enter to keep current value):[/]");
                AnsiConsole.WriteLine();

                // Allow updating individual fields with current values as defaults
                return await UpdateExistingJiraConfigAsync(config, existingConfig);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Configuring completely new Jira settings...[/]");
                AnsiConsole.WriteLine();
            }
        }

        // Get Jira domain
        AnsiConsole.MarkupLine("[bold]Step 1: Jira Domain[/]");
        AnsiConsole.MarkupLine("Find your Jira domain by looking at the URL when you're logged into Jira.");
        AnsiConsole.MarkupLine("[dim]Example: https://mycompany.atlassian.net[/]");
        AnsiConsole.WriteLine();

        string jiraDomain;
        do
        {
            jiraDomain = AnsiConsole.Ask<string>("Enter your Jira domain:", existingBaseUrl ?? "");
            if (!jiraDomain.StartsWith("http"))
            {
                jiraDomain = "https://" + jiraDomain;
            }
            if (!jiraDomain.Contains("atlassian.net"))
            {
                AnsiConsole.MarkupLine("[red]❌ Please enter a valid Atlassian domain (should contain 'atlassian.net')[/]");
                continue;
            }
            break;
        } while (true);

        // Get username
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 2: Atlassian Email[/]");
        AnsiConsole.MarkupLine("Enter the email address you use to log into Jira.");
        string username = AnsiConsole.Ask<string>("Atlassian email:", existingUsername ?? "");

        // Get API Token
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 3: API Token[/]");

        string apiToken;
        if (!string.IsNullOrEmpty(existingApiToken) && existingApiToken != "your-api-token")
        {
            var tokenChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("API Token:")
                    .AddChoices("Keep existing token", "Enter new token"));

            if (tokenChoice == "Keep existing token")
            {
                apiToken = existingApiToken;
                AnsiConsole.MarkupLine("[green]✅ Using existing API token[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("Create a new API token for secure authentication.");
                AnsiConsole.MarkupLine("[bold yellow]📝 Instructions:[/]");
                AnsiConsole.MarkupLine("1. Open: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
                AnsiConsole.MarkupLine("2. Click [bold]'Create API token'[/]");
                AnsiConsole.MarkupLine("3. Label it: [bold]'Tasked Application'[/]");
                AnsiConsole.MarkupLine("4. [bold red]⚠️  Copy the token immediately - you can't see it again![/]");
                AnsiConsole.WriteLine();

                if (!AnsiConsole.Confirm("Have you created your new API token and copied it?"))
                {
                    AnsiConsole.MarkupLine("[yellow]Please create the API token first, then run setup again.[/]");
                    return false;
                }

                apiToken = AnsiConsole.Prompt(
                    new TextPrompt<string>("Paste your new API token:")
                        .Secret('*'));
            }
        }
        else
        {
            AnsiConsole.MarkupLine("You need to create an API token for secure authentication.");
            AnsiConsole.MarkupLine("[bold yellow]📝 Instructions:[/]");
            AnsiConsole.MarkupLine("1. Open: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
            AnsiConsole.MarkupLine("2. Click [bold]'Create API token'[/]");
            AnsiConsole.MarkupLine("3. Label it: [bold]'Tasked Application'[/]");
            AnsiConsole.MarkupLine("4. [bold red]⚠️  Copy the token immediately - you can't see it again![/]");
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("Have you created your API token and copied it?"))
            {
                AnsiConsole.MarkupLine("[yellow]Please create the API token first, then run setup again.[/]");
                return false;
            }

            apiToken = AnsiConsole.Prompt(
                new TextPrompt<string>("Paste your API token:")
                    .Secret('*'));
        }

        // Get JQL Query
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 3: Task Query[/]");
        AnsiConsole.MarkupLine("This JQL (Jira Query Language) query determines which tasks to sync.");
        AnsiConsole.MarkupLine("[dim]The default query gets all tasks assigned to you with relevant statuses.[/]");
        AnsiConsole.WriteLine();

        var defaultJql = "assignee = currentUser() AND status IN (\"To Do\", \"In Progress\", \"Ready for Development\")";
        var useDefaultJql = AnsiConsole.Confirm($"Use default query (recommended for most users)?");

        string jqlQuery;
        if (useDefaultJql)
        {
            jqlQuery = defaultJql;
            AnsiConsole.MarkupLine("[green]✅ Using default query - will sync all your assigned tasks[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Advanced: Custom JQL Query[/]");
            AnsiConsole.MarkupLine("Examples:");
            AnsiConsole.MarkupLine("[dim]• project = 'MYPROJ' AND assignee = currentUser()[/]");
            AnsiConsole.MarkupLine("[dim]• assignee = currentUser() AND labels = 'urgent'[/]");
            AnsiConsole.MarkupLine("[dim]• assignee = currentUser() AND sprint in openSprints()[/]");
            AnsiConsole.WriteLine();

            jqlQuery = AnsiConsole.Ask<string>("Enter your custom JQL query:", defaultJql);
            if (string.IsNullOrWhiteSpace(jqlQuery))
            {
                jqlQuery = defaultJql;
                AnsiConsole.MarkupLine("[yellow]Empty query provided, using default[/]");
            }
        }

        // Store Jira config
        config["Jira"] = new Dictionary<string, object>
        {
            ["BaseUrl"] = jiraDomain,
            ["Username"] = username,
            ["ApiToken"] = apiToken,
            ["JqlQuery"] = jqlQuery,
            ["SyncIntervalMinutes"] = 15
        };

        await SaveConfigurationSectionAsync("Jira", config["Jira"]);
        AnsiConsole.MarkupLine("[green]✅ Jira configuration completed![/]");
        return true;
    }

    private async Task<bool> UpdateExistingJiraConfigAsync(Dictionary<string, object> config, IConfiguration existingConfig)
    {
        var existingBaseUrl = existingConfig["Jira:BaseUrl"];
        var existingUsername = existingConfig["Jira:Username"];
        var existingApiToken = existingConfig["Jira:ApiToken"];
        var existingJqlQuery = existingConfig["Jira:JqlQuery"];

        // Domain
        AnsiConsole.MarkupLine("[bold]Jira Domain[/]");
        string jiraDomain;
        do
        {
            jiraDomain = AnsiConsole.Ask<string>("Enter your Jira domain:", existingBaseUrl ?? "");
            if (string.IsNullOrWhiteSpace(jiraDomain))
            {
                jiraDomain = existingBaseUrl!;
                break;
            }
            if (!jiraDomain.StartsWith("http"))
            {
                jiraDomain = "https://" + jiraDomain;
            }
            if (!jiraDomain.Contains("atlassian.net"))
            {
                AnsiConsole.MarkupLine("[red]❌ Please enter a valid Atlassian domain (should contain 'atlassian.net')[/]");
                continue;
            }
            break;
        } while (true);

        // Username
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Atlassian Email[/]");
        string username = AnsiConsole.Ask<string>("Atlassian email:", existingUsername ?? "");
        if (string.IsNullOrWhiteSpace(username))
            username = existingUsername!;

        // API Token
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]API Token[/]");
        string apiToken;

        var tokenChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("API Token:")
                .AddChoices("Keep current token", "Enter new token"));

        if (tokenChoice == "Keep current token")
        {
            apiToken = existingApiToken!;
            AnsiConsole.MarkupLine("[green]✅ Using existing API token[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("Create a new API token at: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
            if (!AnsiConsole.Confirm("Have you created your new API token?"))
            {
                AnsiConsole.MarkupLine("[yellow]Please create the API token first, then run setup again.[/]");
                return false;
            }
            apiToken = AnsiConsole.Prompt(new TextPrompt<string>("Paste your new API token:").Secret('*'));
        }

        // JQL Query
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Task Query (JQL)[/]");
        var defaultJql = "assignee = currentUser() AND status IN (\"To Do\", \"In Progress\", \"Ready for Development\")";
        var currentJql = existingJqlQuery ?? defaultJql;

        var jqlChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("JQL Query:")
                .AddChoices("Keep current query", "Use default query", "Enter custom query"));

        string jqlQuery;
        if (jqlChoice == "Keep current query")
        {
            jqlQuery = currentJql;
            AnsiConsole.MarkupLine("[green]✅ Using existing JQL query[/]");
        }
        else if (jqlChoice == "Use default query")
        {
            jqlQuery = defaultJql;
            AnsiConsole.MarkupLine("[green]✅ Using default JQL query[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"Current query: [dim]{currentJql}[/]");
            jqlQuery = AnsiConsole.Ask<string>("Enter your custom JQL query:", currentJql);
            if (string.IsNullOrWhiteSpace(jqlQuery))
                jqlQuery = currentJql;
        }

        // Store updated config
        config["Jira"] = new Dictionary<string, object>
        {
            ["BaseUrl"] = jiraDomain,
            ["Username"] = username,
            ["ApiToken"] = apiToken,
            ["JqlQuery"] = jqlQuery,
            ["SyncIntervalMinutes"] = existingConfig.GetValue<int>("Jira:SyncIntervalMinutes", 15)
        };

        await SaveConfigurationSectionAsync("Jira", config["Jira"]);
        AnsiConsole.MarkupLine("[green]✅ Jira configuration updated![/]");
        return true;
    }    private async Task<bool> SetupRepositoryAsync(Dictionary<string, object> config)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]🔧 Repository Provider Selection[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Tasked can connect to various repository providers.");
        AnsiConsole.MarkupLine("[dim]Currently supported: BitBucket (GitLab and GitHub support coming soon)[/]");
        AnsiConsole.WriteLine();

        var provider = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select your repository provider:")
                .AddChoices("BitBucket", "Skip repository setup for now"));

        if (provider == "Skip repository setup for now")
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Skipping repository setup. You can configure this later in appsettings.local.json[/]");
            return true;
        }

        if (provider == "BitBucket")
        {
            // Show specific provider configuration header
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold blue]🔧 BitBucket Configuration[/]").RuleStyle("blue"));
            AnsiConsole.WriteLine();

            return await SetupBitBucketAsync(config);
        }

        return true;
    }

    private async Task<bool> SetupBitBucketAsync(Dictionary<string, object> config)
    {
        // BitBucket authentication transition dates
        var appPasswordEndDate = new DateTime(2025, 9, 9);
        var appPasswordInactiveDate = new DateTime(2026, 6, 9);
        var today = DateTime.UtcNow;

        AnsiConsole.MarkupLine("[bold yellow]BitBucket authentication is transitioning from App Passwords to API Tokens.[/]");
        AnsiConsole.MarkupLine("• [bold]App Password creation ends:[/] September 9, 2025");
        AnsiConsole.MarkupLine("• [bold]App Passwords become inactive:[/] June 9, 2026");
        AnsiConsole.MarkupLine("• [bold]API Tokens are the new standard for BitBucket authentication.[/]");
        AnsiConsole.WriteLine();

        string? bitbucketCredential = null;
        string? bitbucketUsername = null;
        var jiraApiToken = config.ContainsKey("Jira") && config["Jira"] is Dictionary<string, object> jiraDict && jiraDict.ContainsKey("ApiToken")
            ? jiraDict["ApiToken"]?.ToString()
            : null;

        bool useAppPassword = today < appPasswordEndDate;
        if (useAppPassword)
        {
            AnsiConsole.MarkupLine("[bold yellow]You can use either an App Password or an API Token for BitBucket until September 9, 2025.[/]");
            var tokenType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose BitBucket authentication method:")
                    .AddChoices("App Password", "API Token"));

            if (tokenType == "App Password")
            {
                AnsiConsole.MarkupLine("Create an App Password at: [link]https://bitbucket.org/account/settings/app-passwords/[/]");
                AnsiConsole.MarkupLine("[bold red]⚠️  App Passwords will stop working after June 9, 2026.[/]");

                // Get BitBucket username for App Password authentication
                bitbucketUsername = AnsiConsole.Ask<string>("Enter your BitBucket username:");
                bitbucketCredential = AnsiConsole.Prompt(new TextPrompt<string>("Paste your BitBucket App Password:").Secret('*'));
            }
            else
            {
                AnsiConsole.MarkupLine("Create an API Token at: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
                if (!string.IsNullOrEmpty(jiraApiToken))
                {
                    var useJiraToken = AnsiConsole.Confirm("Use your existing Jira API Token for BitBucket?");
                    if (useJiraToken)
                        bitbucketCredential = jiraApiToken;
                }
                if (string.IsNullOrEmpty(bitbucketCredential))
                    bitbucketCredential = AnsiConsole.Prompt(new TextPrompt<string>("Paste your BitBucket API Token:").Secret('*'));
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[bold yellow]App Password creation is disabled. You must use an API Token for BitBucket.[/]");
            AnsiConsole.MarkupLine("Create an API Token at: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
            if (!string.IsNullOrEmpty(jiraApiToken))
            {
                var useJiraToken = AnsiConsole.Confirm("Use your existing Jira API Token for BitBucket?");
                if (useJiraToken)
                    bitbucketCredential = jiraApiToken;
            }
            if (string.IsNullOrEmpty(bitbucketCredential))
                bitbucketCredential = AnsiConsole.Prompt(new TextPrompt<string>("Paste your BitBucket API Token:").Secret('*'));
        }

        // Check for existing BitBucket configuration first
        var existingConfig = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(_configPath, optional: false)
            .AddJsonFile(_localConfigPath, optional: true)
            .Build();

        var existingWorkspace = existingConfig["RepositoryProviders:0:WorkspaceOrOrganization"];
        var existingAppPassword = existingConfig["RepositoryProviders:0:AppPassword"];
        var existingApiToken = existingConfig["RepositoryProviders:0:ApiToken"];
        var existingProjectKey = existingConfig["RepositoryProviders:0:AdditionalSettings:ProjectKey"];

        string existingCredential = useAppPassword ? (existingAppPassword ?? "") : (existingApiToken ?? "");
        bool hasExistingConfig = !string.IsNullOrEmpty(existingCredential) &&
                                existingCredential != "your-bitbucket-token" &&
                                existingCredential != "REPLACE IN appsettings.local.json" &&
                                !string.IsNullOrEmpty(existingWorkspace) &&
                                existingWorkspace != "your-workspace" &&
                                existingWorkspace != "REPLACE IN appsettings.local.json";

        if (hasExistingConfig)
        {
            AnsiConsole.MarkupLine("[green]🔧 Existing BitBucket configuration detected![/]");
            AnsiConsole.MarkupLine($"[dim]Workspace: {existingWorkspace}[/]");
            if (!string.IsNullOrEmpty(existingProjectKey))
                AnsiConsole.MarkupLine($"[dim]Project Key: {existingProjectKey}[/]");
            AnsiConsole.MarkupLine($"[dim]{(useAppPassword ? "App Password" : "API Token")}: ****...{existingCredential?.Substring(Math.Max(0, existingCredential.Length - 8))}[/]");
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]What would you like to do?[/]")
                    .AddChoices(
                        "Keep existing configuration",
                        "Edit existing values (keep what you want)",
                        "Start over with fresh configuration"));

            if (choice == "Keep existing configuration")
            {
                // Preserve existing configuration exactly as-is
                var existingRepoConfig = new Dictionary<string, object>
                {
                    ["Name"] = "BitBucket",
                    ["BaseUrl"] = "https://api.bitbucket.org",
                    [useAppPassword ? "AppPassword" : "ApiToken"] = existingCredential!,
                    ["WorkspaceOrOrganization"] = existingWorkspace!,
                    ["IsDefault"] = true
                };

                if (!string.IsNullOrEmpty(existingProjectKey))
                {
                    existingRepoConfig["AdditionalSettings"] = new Dictionary<string, object>
                    {
                        ["ProjectKey"] = existingProjectKey
                    };
                }

                config["RepositoryProviders"] = new[] { existingRepoConfig };

                await SaveConfigurationSectionAsync("RepositoryProviders", config["RepositoryProviders"]);
                AnsiConsole.MarkupLine($"[green]✅ Keeping existing BitBucket configuration using {(useAppPassword ? "App Password" : "API Token")}![/]");
                return true;
            }
            else if (choice == "Edit existing values (keep what you want)")
            {
                AnsiConsole.MarkupLine("[yellow]You can update individual fields (press Enter to keep current value):[/]");
                AnsiConsole.WriteLine();

                // Allow updating individual fields
                return await UpdateExistingBitBucketConfigAsync(config, existingConfig);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Configuring completely new BitBucket settings...[/]");
                AnsiConsole.WriteLine();
            }
        }

        // Prompt for workspace name
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 2: Workspace Name[/]");
        AnsiConsole.MarkupLine("Find your workspace name in BitBucket URLs.");
        AnsiConsole.MarkupLine("[dim]Example: In 'https://bitbucket.org/myworkspace/repo', the workspace is 'myworkspace'[/]");
        string workspace = AnsiConsole.Ask<string>("Enter your BitBucket workspace name:", existingWorkspace ?? "");

        // Optional: Project Key
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 3: Project Settings (Optional)[/]");
        var hasProject = AnsiConsole.Confirm("Do you use BitBucket Projects? (optional)",
            !string.IsNullOrEmpty(existingProjectKey));
        string? projectKey = null;
        if (hasProject)
        {
            projectKey = AnsiConsole.Ask<string>("Enter your BitBucket project key:", existingProjectKey ?? "");
        }

        // Store repository config
        var repoConfig = new Dictionary<string, object>
        {
            ["Name"] = "BitBucket",
            ["BaseUrl"] = "https://api.bitbucket.org",
            [useAppPassword ? "AppPassword" : "ApiToken"] = bitbucketCredential,
            ["WorkspaceOrOrganization"] = workspace,
            ["IsDefault"] = true
        };

        // Add BitBucket username for App Password authentication
        if (useAppPassword && !string.IsNullOrEmpty(bitbucketUsername))
        {
            repoConfig["Username"] = bitbucketUsername;
        }

        if (!string.IsNullOrEmpty(projectKey))
        {
            repoConfig["AdditionalSettings"] = new Dictionary<string, object>
            {
                ["ProjectKey"] = projectKey
            };
        }

        config["RepositoryProviders"] = new[] { repoConfig };

        await SaveConfigurationSectionAsync("RepositoryProviders", config["RepositoryProviders"]);
        AnsiConsole.MarkupLine($"[green]✅ BitBucket configuration completed using {(useAppPassword ? "App Password" : "API Token")}![/]");
        return true;
    }

    private async Task<bool> UpdateExistingBitBucketConfigAsync(Dictionary<string, object> config, IConfiguration existingConfig)
    {
        var existingWorkspace = existingConfig["RepositoryProviders:0:WorkspaceOrOrganization"];
        var existingToken = existingConfig["RepositoryProviders:0:ApiToken"];
        var existingProjectKey = existingConfig["RepositoryProviders:0:AdditionalSettings:ProjectKey"];

        // API Token
        AnsiConsole.MarkupLine("[bold]API Token[/]");
        string apiToken;

        var tokenChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("API Token:")
                .AddChoices("Keep current API Token", "Enter new API Token"));

        if (tokenChoice == "Keep current API Token")
        {
            apiToken = existingToken!;
            AnsiConsole.MarkupLine("[green]✅ Using existing API Token[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("Create a new API Token at: [link]https://id.atlassian.com/manage-profile/security/api-tokens[/]");
            AnsiConsole.MarkupLine("[bold red]⚠️  Copy the token immediately - you can't see it again![/]");
            if (!AnsiConsole.Confirm("Have you created your new API Token?"))
            {
                AnsiConsole.MarkupLine("[yellow]Please create the API Token first, then run setup again.[/]");
                return false;
            }
            apiToken = AnsiConsole.Prompt(new TextPrompt<string>("Paste your new API Token:").Secret('*'));
        }

        // Workspace (auto-discover or keep existing)
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]BitBucket Workspace[/]");

        // Prompt for workspace name
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]BitBucket Workspace Name[/]");
        AnsiConsole.MarkupLine("Find your workspace name in BitBucket URLs.");
        AnsiConsole.MarkupLine("[dim]Example: In 'https://bitbucket.org/myworkspace/repo', the workspace is 'myworkspace'[/]");
        var workspaceDefault = string.IsNullOrEmpty(existingWorkspace) ||
            existingWorkspace == "REPLACE IN appsettings.local.json" ||
            existingWorkspace == "your-workspace"
            ? ""
            : existingWorkspace;
        string workspace = AnsiConsole.Ask<string>("Enter your BitBucket workspace name:", workspaceDefault);

        // Project Key (optional)
        AnsiConsole.WriteLine();
        var currentHasProject = !string.IsNullOrEmpty(existingProjectKey);
        var hasProject = AnsiConsole.Confirm("Do you use BitBucket Projects? (optional)", currentHasProject);
        string? projectKey = null;
        if (hasProject)
        {
            projectKey = AnsiConsole.Ask<string>("Enter your BitBucket project key:", existingProjectKey ?? "");
            if (string.IsNullOrWhiteSpace(projectKey) && !string.IsNullOrEmpty(existingProjectKey))
                projectKey = existingProjectKey;
        }

        // Store updated config
        var repoConfig = new Dictionary<string, object>
        {
            ["Name"] = "BitBucket",
            ["BaseUrl"] = "https://api.bitbucket.org",
            ["ApiToken"] = apiToken,
            ["WorkspaceOrOrganization"] = workspace,
            ["IsDefault"] = true
        };

        if (!string.IsNullOrEmpty(projectKey))
        {
            repoConfig["AdditionalSettings"] = new Dictionary<string, object>
            {
                ["ProjectKey"] = projectKey
            };
        }

        config["RepositoryProviders"] = new[] { repoConfig };

        await SaveConfigurationSectionAsync("RepositoryProviders", config["RepositoryProviders"]);
        AnsiConsole.MarkupLine("[green]✅ BitBucket configuration updated![/]");
        return true;
    }    private async Task<List<(string Name, string Slug, int RepoCount)>> FetchBitBucketWorkspacesAsync(string appPassword, string username)
    {
        var workspaces = new List<(string Name, string Slug, int RepoCount)>();

        try
        {
            using var client = new HttpClient();
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{appPassword}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            // Fetch workspaces
            var response = await client.GetAsync("https://api.bitbucket.org/2.0/workspaces?role=member&pagelen=100");

            if (!response.IsSuccessStatusCode)
            {
                return workspaces;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);

            if (json.RootElement.TryGetProperty("values", out var values))
            {
                foreach (var workspace in values.EnumerateArray())
                {
                    if (workspace.TryGetProperty("name", out var nameElement) &&
                        workspace.TryGetProperty("slug", out var slugElement))
                    {
                        var name = nameElement.GetString() ?? "";
                        var slug = slugElement.GetString() ?? "";

                        // Get repository count for this workspace
                        var repoCount = await GetRepositoryCountAsync(client, slug);

                        workspaces.Add((name, slug, repoCount));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail setup - user can enter manually
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not fetch workspaces automatically: {ex.Message}[/]");
        }

        return workspaces.OrderByDescending(w => w.RepoCount).ToList();
    }

    private async Task<int> GetRepositoryCountAsync(HttpClient client, string workspace)
    {
        try
        {
            var response = await client.GetAsync($"https://api.bitbucket.org/2.0/repositories/{workspace}?role=member&pagelen=1");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("size", out var sizeElement))
                {
                    return sizeElement.GetInt32();
                }
            }
        }
        catch
        {
            // Ignore errors for repo count
        }
        return 0;
    }

    private async Task SaveConfigurationSectionAsync(string sectionName, object sectionData)
    {
        try
        {
            // Load existing configuration
            var existingJson = "{}";
            if (File.Exists(_localConfigPath))
            {
                existingJson = await File.ReadAllTextAsync(_localConfigPath);
            }

            var existingConfig = JsonDocument.Parse(existingJson);
            var configDict = new Dictionary<string, object>();

            // Copy existing sections
            foreach (var property in existingConfig.RootElement.EnumerateObject())
            {
                configDict[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText())!;
            }

            // Update the specific section
            configDict[sectionName] = sectionData;

            // Save back to file
            var json = JsonSerializer.Serialize(configDict, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_localConfigPath, json);

            AnsiConsole.MarkupLine($"[green]✅ {sectionName} configuration saved![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error saving {sectionName} configuration: {ex.Message}[/]");
            throw;
        }
    }

    private async Task SetupWorkflowAsync(Dictionary<string, object> config)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]⚙️ Workflow Settings[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Configure how Tasked should behave when processing tasks.");
        AnsiConsole.WriteLine();

        var requireApproval = AnsiConsole.Confirm(
            "[bold]Require human approval for automated actions?[/]\n" +
            "[dim](Recommended: Yes for initial setup, No after you trust the automation)[/]",
            defaultValue: true);

        var autoCreateBranches = AnsiConsole.Confirm(
            "[bold]Automatically create feature branches?[/]\n" +
            "[dim](Recommended: Yes - creates branches like 'feature/PROJ-123-task-title')[/]",
            defaultValue: true);

        var autoUpdateJira = AnsiConsole.Confirm(
            "[bold]Automatically update Jira with progress?[/]\n" +
            "[dim](Recommended: Yes - adds comments to Jira issues)[/]",
            defaultValue: true);

        var maxTasks = AnsiConsole.Prompt(
            new TextPrompt<int>("[bold]Maximum concurrent tasks to process?[/]")
                .DefaultValue(3)
                .ValidationErrorMessage("Please enter a number between 1 and 10")
                .Validate(x => x >= 1 && x <= 10));

        config["Workflow"] = new Dictionary<string, object>
        {
            ["RequireHumanApproval"] = requireApproval,
            ["AutoCreateRepositories"] = false, // Always start safe
            ["AutoCreateBranches"] = autoCreateBranches,
            ["AutoUpdateJira"] = autoUpdateJira,
            ["MaxConcurrentTasks"] = maxTasks
        };

        config["Database"] = new Dictionary<string, object>
        {
            ["ConnectionString"] = "Data Source=tasked.db"
        };

        await SaveConfigurationSectionAsync("Workflow", config["Workflow"]);
        await SaveConfigurationSectionAsync("Database", config["Database"]);
        AnsiConsole.MarkupLine("[green]✅ Workflow configuration completed![/]");
    }

    private async Task SaveConfigurationAsync(Dictionary<string, object> config)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .Start("Saving configuration...", ctx =>
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_localConfigPath, json);
            });

        AnsiConsole.MarkupLine($"[green]✅ Configuration saved to {_localConfigPath}[/]");
        AnsiConsole.MarkupLine("[dim]This file is excluded from version control to keep your credentials secure.[/]");

        await Task.CompletedTask; // Satisfy async requirement
    }

    private async Task TestConnectionsAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]🧪 Testing Connections[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Testing your configuration...");
        AnsiConsole.WriteLine();

        try
        {
            // Use the new TokenHealthService for testing
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_configPath, optional: false)
                .AddJsonFile(_localConfigPath, optional: true)
                .Build();

            var httpClient = new HttpClient();
            var tokenHealth = new TokenHealthService(config,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<TokenHealthService>(),
                httpClient);

            var report = await tokenHealth.CheckAllTokensAsync();

            // Show results
            if (report.JiraStatus == TokenStatus.Valid &&
                (report.RepositoryStatus == TokenStatus.Valid || report.RepositoryStatus == TokenStatus.NotConfigured))
            {
                AnsiConsole.MarkupLine("[green]✅ All configured tokens are working correctly![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Some issues detected with your tokens.[/]");
                AnsiConsole.MarkupLine("[dim]Run 'dotnet run --token-health' after setup for detailed analysis.[/]");
            }

            httpClient.Dispose();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Could not test connections at this time.[/]");
            AnsiConsole.MarkupLine($"[dim]Error: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]You can test manually later with 'dotnet run --token-health'[/]");
        }
    }
}
