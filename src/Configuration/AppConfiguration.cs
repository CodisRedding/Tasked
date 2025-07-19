namespace Tasked.Configuration;

public class AppConfiguration
{
    public JiraConfiguration Jira { get; set; } = new();
    public DatabaseConfiguration Database { get; set; } = new();
    public List<RepositoryProviderConfiguration> RepositoryProviders { get; set; } = new();
    public WorkflowConfiguration Workflow { get; set; } = new();
}

public class JiraConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string JqlQuery { get; set; } = "assignee = currentUser() AND status IN (\"To Do\", \"In Progress\", \"Ready for Development\")";
    public int SyncIntervalMinutes { get; set; } = 15;
}

public class DatabaseConfiguration
{
    public string ConnectionString { get; set; } = "Data Source=tasked.db";
}

public class RepositoryProviderConfiguration
{
    public string Name { get; set; } = string.Empty; // BitBucket, GitLab, etc.
    public string BaseUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty; // Legacy - for backward compatibility
    public string AppPassword { get; set; } = string.Empty; // BitBucket App Password (until Sept 2025)
    public string ApiToken { get; set; } = string.Empty; // BitBucket API Token (after Sept 2025)
    public string Username { get; set; } = string.Empty; // BitBucket username
    public string? WorkspaceOrOrganization { get; set; }
    public bool IsDefault { get; set; } = false;
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}

public class WorkflowConfiguration
{
    public bool RequireHumanApproval { get; set; } = true;
    public bool AutoCreateRepositories { get; set; } = false;
    public bool AutoCreateBranches { get; set; } = true;
    public bool AutoUpdateJira { get; set; } = true;
    public int MaxConcurrentTasks { get; set; } = 3;
}
