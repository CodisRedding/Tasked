namespace Tasked.Models;

public class TaskProgress
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;

    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProgressType Type { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.Pending;

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AdditionalData { get; set; } // JSON for any extra data
}

public enum ProgressType
{
    JiraSync,
    RepositoryCreation,
    RepositoryAssignment,
    BranchCreation,
    CodeGeneration,
    Testing,
    PullRequest,
    Deployment,
    HumanReview
}

public enum ProgressStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    AwaitingApproval,
    Approved,
    Rejected
}
