namespace Tasked.Models;

public class Repository
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public RepositoryProvider Provider { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Configuration
    public string? DefaultBranch { get; set; } = "main";
    public string? AccessToken { get; set; } // Encrypted
    public string? ProjectKey { get; set; } // For GitLab projects, BitBucket workspaces, etc.
}

public enum RepositoryProvider
{
    BitBucket,
    GitLab,
    GitHub,
    AzureDevOps
}
