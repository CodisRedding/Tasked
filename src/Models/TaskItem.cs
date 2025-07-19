namespace Tasked.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string JiraKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Reporter { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? DueDate { get; set; }

    // Local tracking fields
    public TaskStatus LocalStatus { get; set; } = TaskStatus.New;
    public string? RepositoryUrl { get; set; }
    public string? BranchName { get; set; }
    public string? Notes { get; set; }
    public bool RequiresHumanApproval { get; set; } = false;
    public DateTime LastSyncedAt { get; set; }

    // Navigation properties
    public List<TaskProgress> ProgressHistory { get; set; } = new();
}

public enum TaskStatus
{
    New,
    InProgress,
    AwaitingApproval,
    Approved,
    Rejected,
    Completed,
    Blocked
}
