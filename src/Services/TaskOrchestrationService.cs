using Microsoft.Extensions.Logging;
using Tasked.Data;
using Tasked.Models;
using Tasked.Services;
using Microsoft.EntityFrameworkCore;
using TaskStatus = Tasked.Models.TaskStatus;

namespace Tasked.Services;

public interface ITaskOrchestrationService
{
    Task ProcessNewTasksAsync();
    Task ProcessPendingTasksAsync();
    Task SyncWithJiraAsync();
    Task<TaskItem?> GetTaskAsync(int id);
    Task<List<TaskItem>> GetTasksAsync(TaskStatus? status = null);
    Task<bool> ApproveTaskProgressAsync(int taskId, int progressId);
    Task<bool> RejectTaskProgressAsync(int taskId, int progressId, string reason);
}

public class TaskOrchestrationService : ITaskOrchestrationService
{
    private readonly TaskedDbContext _context;
    private readonly IJiraService _jiraService;
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<TaskOrchestrationService> _logger;

    public TaskOrchestrationService(
        TaskedDbContext context,
        IJiraService jiraService,
        IRepositoryService repositoryService,
        ILogger<TaskOrchestrationService> logger)
    {
        _context = context;
        _jiraService = jiraService;
        _repositoryService = repositoryService;
        _logger = logger;
    }

    public async Task ProcessNewTasksAsync()
    {
        var newTasks = await _context.Tasks
            .Where(t => t.LocalStatus == TaskStatus.New)
            .ToListAsync();

        foreach (var task in newTasks)
        {
            try
            {
                await ProcessSingleTaskAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing task {task.JiraKey}");
                AddTaskProgressAsync(task.Id, "Error Processing",
                    ex.Message, ProgressType.HumanReview, ProgressStatus.Failed);
            }
        }
    }

    public async Task ProcessPendingTasksAsync()
    {
        var pendingTasks = await _context.Tasks
            .Include(t => t.ProgressHistory)
            .Where(t => t.LocalStatus == TaskStatus.InProgress)
            .ToListAsync();

        foreach (var task in pendingTasks)
        {
            try
            {
                await ContinueTaskProcessingAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error continuing task {task.JiraKey}");
            }
        }
    }

    public async Task SyncWithJiraAsync()
    {
        try
        {
            _logger.LogInformation("Starting Jira sync...");
            var jiraTasks = await _jiraService.GetTasksAsync();

            foreach (var jiraTask in jiraTasks)
            {
                var existingTask = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.JiraKey == jiraTask.JiraKey);

                if (existingTask == null)
                {
                    // New task from Jira
                    _context.Tasks.Add(jiraTask);
                    _logger.LogInformation($"Added new task from Jira: {jiraTask.JiraKey}");
                }
                else
                {
                    // Update existing task
                    existingTask.Title = jiraTask.Title;
                    existingTask.Description = jiraTask.Description;
                    existingTask.Status = jiraTask.Status;
                    existingTask.Priority = jiraTask.Priority;
                    existingTask.Assignee = jiraTask.Assignee;
                    existingTask.UpdatedDate = jiraTask.UpdatedDate;
                    existingTask.DueDate = jiraTask.DueDate;
                    existingTask.LastSyncedAt = DateTime.UtcNow;

                    _logger.LogInformation($"Updated task from Jira: {jiraTask.JiraKey}");
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Jira sync completed. Processed {jiraTasks.Count} tasks.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Jira sync");
        }
    }

    public async Task<TaskItem?> GetTaskAsync(int id)
    {
        return await _context.Tasks
            .Include(t => t.ProgressHistory)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<TaskItem>> GetTasksAsync(TaskStatus? status = null)
    {
        var query = _context.Tasks.Include(t => t.ProgressHistory).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.LocalStatus == status.Value);
        }

        return await query.OrderByDescending(t => t.CreatedDate).ToListAsync();
    }

    public async Task<bool> ApproveTaskProgressAsync(int taskId, int progressId)
    {
        var task = await GetTaskAsync(taskId);
        var progress = task?.ProgressHistory.FirstOrDefault(p => p.Id == progressId);

        if (task == null || progress == null)
            return false;

        progress.Status = ProgressStatus.Approved;
        progress.CompletedAt = DateTime.UtcNow;

        // Continue with next step in the workflow
        await ContinueTaskProcessingAsync(task);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectTaskProgressAsync(int taskId, int progressId, string reason)
    {
        var task = await GetTaskAsync(taskId);
        var progress = task?.ProgressHistory.FirstOrDefault(p => p.Id == progressId);

        if (task == null || progress == null)
            return false;

        progress.Status = ProgressStatus.Rejected;
        progress.CompletedAt = DateTime.UtcNow;
        progress.ErrorMessage = reason;

        task.LocalStatus = TaskStatus.Blocked;

        await _context.SaveChangesAsync();
        return true;
    }

    private async Task ProcessSingleTaskAsync(TaskItem task)
    {
        _logger.LogInformation($"Processing new task: {task.JiraKey} - {task.Title}");

        task.LocalStatus = TaskStatus.InProgress;
        await _context.SaveChangesAsync();

        // Step 1: Find or suggest repository
        AddTaskProgressAsync(task.Id, "Finding Repository",
            "Analyzing task to find suitable repository", ProgressType.RepositoryAssignment, ProgressStatus.InProgress);

        var repository = await _repositoryService.FindSuitableRepositoryAsync(task);

        if (repository == null)
        {
            AddTaskProgressAsync(task.Id, "Repository Assignment",
                "No suitable repository found. Human intervention required.",
                ProgressType.RepositoryAssignment, ProgressStatus.AwaitingApproval);
            task.RequiresHumanApproval = true;
        }
        else
        {
            task.RepositoryUrl = repository.Url;
            AddTaskProgressAsync(task.Id, "Repository Assignment",
                $"Assigned to repository: {repository.Name}",
                ProgressType.RepositoryAssignment, ProgressStatus.Completed);

            // Step 2: Create branch
            await CreateBranchForTaskAsync(task, repository);
        }

        await _context.SaveChangesAsync();
    }

    private async Task ContinueTaskProcessingAsync(TaskItem task)
    {
        var lastProgress = task.ProgressHistory
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

        if (lastProgress?.Status == ProgressStatus.AwaitingApproval)
        {
            // Task is waiting for human approval, don't continue
            return;
        }

        // Continue based on the current state
        if (lastProgress?.Type == ProgressType.RepositoryAssignment &&
            lastProgress.Status == ProgressStatus.Completed)
        {
            // Next step: Create branch
            var repository = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Url == task.RepositoryUrl);

            if (repository != null)
            {
                await CreateBranchForTaskAsync(task, repository);
            }
        }
        // Add more workflow steps here as needed
    }

    private async Task CreateBranchForTaskAsync(TaskItem task, Repository repository)
    {
        try
        {
            var branchName = GenerateBranchName(task);
            task.BranchName = branchName;

            AddTaskProgressAsync(task.Id, "Creating Branch",
                $"Creating branch: {branchName}", ProgressType.BranchCreation, ProgressStatus.InProgress);

            var success = await _repositoryService.CreateBranchAsync(repository, branchName);

            if (success)
            {
                AddTaskProgressAsync(task.Id, "Branch Creation",
                    $"Successfully created branch: {branchName}",
                    ProgressType.BranchCreation, ProgressStatus.Completed);

                // Update Jira with progress
                await _jiraService.AddCommentAsync(task.JiraKey,
                    $"Created development branch: {branchName} in repository {repository.Name}");
            }
            else
            {
                AddTaskProgressAsync(task.Id, "Branch Creation",
                    "Failed to create branch. Human intervention required.",
                    ProgressType.BranchCreation, ProgressStatus.AwaitingApproval);
                task.RequiresHumanApproval = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating branch for task {task.JiraKey}");
            AddTaskProgressAsync(task.Id, "Branch Creation",
                $"Error creating branch: {ex.Message}",
                ProgressType.BranchCreation, ProgressStatus.Failed);
        }
    }

    private void AddTaskProgressAsync(int taskId, string action, string description,
        ProgressType type, ProgressStatus status)
    {
        var progress = new TaskProgress
        {
            TaskItemId = taskId,
            Action = action,
            Description = description,
            Type = type,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        if (status == ProgressStatus.Completed || status == ProgressStatus.Failed)
        {
            progress.CompletedAt = DateTime.UtcNow;
        }

        _context.TaskProgress.Add(progress);
    }

    private static string GenerateBranchName(TaskItem task)
    {
        // Generate a branch name based on the Jira key and task title
        var title = task.Title.ToLower()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Replace(",", "")
            .Replace(":", "")
            .Replace(";", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("{", "")
            .Replace("}", "");

        // Limit length and sanitize
        if (title.Length > 30)
        {
            title = title.Substring(0, 30);
        }

        return $"feature/{task.JiraKey.ToLower()}-{title}";
    }
}
