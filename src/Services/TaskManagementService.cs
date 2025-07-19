using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tasked.Data;
using Tasked.Models;
using TaskStatus = Tasked.Models.TaskStatus;

namespace Tasked.Services;

public interface ITaskManagementService
{
    Task SyncTasksFromJiraAsync();
    Task<List<TaskItem>> GetActiveTasksAsync();
    Task<TaskItem?> GetTaskAsync(int id);
    Task<TaskItem?> GetTaskByJiraKeyAsync(string jiraKey);
    Task<bool> AssignRepositoryToTaskAsync(int taskId, int repositoryId);
    Task<bool> CreateBranchForTaskAsync(int taskId, string? branchName = null);
    Task<bool> UpdateTaskProgressAsync(int taskId, ProgressType progressType, ProgressStatus status, string? description = null);
    Task<List<TaskItem>> GetTasksAwaitingApprovalAsync();
    Task<bool> ApproveTaskAsync(int taskId);
    Task<bool> RejectTaskAsync(int taskId, string reason);
}

public class TaskManagementService : ITaskManagementService
{
    private readonly TaskedDbContext _dbContext;
    private readonly IJiraService _jiraService;
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<TaskManagementService> _logger;

    public TaskManagementService(
        TaskedDbContext dbContext,
        IJiraService jiraService,
        IRepositoryService repositoryService,
        ILogger<TaskManagementService> logger)
    {
        _dbContext = dbContext;
        _jiraService = jiraService;
        _repositoryService = repositoryService;
        _logger = logger;
    }

    public async Task SyncTasksFromJiraAsync()
    {
        try
        {
            _logger.LogInformation("Starting Jira sync...");

            var jiraTasks = await _jiraService.GetTasksAsync();
            var syncedCount = 0;
            var newCount = 0;

            foreach (var jiraTask in jiraTasks)
            {
                var existingTask = await _dbContext.Tasks
                    .FirstOrDefaultAsync(t => t.JiraKey == jiraTask.JiraKey);

                if (existingTask != null)
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

                    syncedCount++;
                }
                else
                {
                    // Add new task
                    _dbContext.Tasks.Add(jiraTask);
                    newCount++;

                    // Create initial progress entry
                    var initialProgress = new TaskProgress
                    {
                        TaskItem = jiraTask,
                        Action = "Task synced from Jira",
                        Description = $"New task '{jiraTask.Title}' imported from Jira",
                        Type = ProgressType.JiraSync,
                        Status = ProgressStatus.Completed,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    };
                    _dbContext.TaskProgress.Add(initialProgress);
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Jira sync completed. New tasks: {newCount}, Updated tasks: {syncedCount}");

            // Process new tasks for repository assignment
            await ProcessNewTasksAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Jira sync");
            throw;
        }
    }

    public async Task<List<TaskItem>> GetActiveTasksAsync()
    {
        return await _dbContext.Tasks
            .Include(t => t.ProgressHistory)
            .Where(t => t.LocalStatus != TaskStatus.Completed)
            .OrderByDescending(t => t.CreatedDate)
            .ToListAsync();
    }

    public async Task<TaskItem?> GetTaskAsync(int id)
    {
        return await _dbContext.Tasks
            .Include(t => t.ProgressHistory)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TaskItem?> GetTaskByJiraKeyAsync(string jiraKey)
    {
        return await _dbContext.Tasks
            .Include(t => t.ProgressHistory)
            .FirstOrDefaultAsync(t => t.JiraKey == jiraKey);
    }

    public async Task<bool> AssignRepositoryToTaskAsync(int taskId, int repositoryId)
    {
        try
        {
            var task = await GetTaskAsync(taskId);
            var repository = await _dbContext.Repositories.FindAsync(repositoryId);

            if (task == null || repository == null)
            {
                _logger.LogWarning($"Task {taskId} or Repository {repositoryId} not found");
                return false;
            }

            task.RepositoryUrl = repository.Url;
            task.LocalStatus = TaskStatus.InProgress;

            await UpdateTaskProgressAsync(taskId, ProgressType.RepositoryAssignment,
                ProgressStatus.Completed, $"Assigned to repository: {repository.Name}");

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Task {taskId} assigned to repository {repository.Name}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error assigning repository to task {taskId}");
            return false;
        }
    }

    public async Task<bool> CreateBranchForTaskAsync(int taskId, string? branchName = null)
    {
        try
        {
            var task = await GetTaskAsync(taskId);
            if (task == null || string.IsNullOrEmpty(task.RepositoryUrl))
            {
                _logger.LogWarning($"Task {taskId} not found or no repository assigned");
                return false;
            }

            var repository = await _dbContext.Repositories
                .FirstOrDefaultAsync(r => r.Url == task.RepositoryUrl);

            if (repository == null)
            {
                _logger.LogWarning($"Repository not found for task {taskId}");
                return false;
            }

            branchName ??= GenerateBranchName(task);

            var success = await _repositoryService.CreateBranchAsync(repository, branchName);
            if (success)
            {
                task.BranchName = branchName;
                await UpdateTaskProgressAsync(taskId, ProgressType.BranchCreation,
                    ProgressStatus.Completed, $"Created branch: {branchName}");
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Branch {branchName} created for task {taskId}");
                return true;
            }

            await UpdateTaskProgressAsync(taskId, ProgressType.BranchCreation,
                ProgressStatus.Failed, "Failed to create branch");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating branch for task {taskId}");
            await UpdateTaskProgressAsync(taskId, ProgressType.BranchCreation,
                ProgressStatus.Failed, $"Error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateTaskProgressAsync(int taskId, ProgressType progressType, ProgressStatus status, string? description = null)
    {
        try
        {
            var progress = new TaskProgress
            {
                TaskItemId = taskId,
                Action = progressType.ToString(),
                Description = description ?? string.Empty,
                Type = progressType,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            if (status == ProgressStatus.Completed || status == ProgressStatus.Failed)
            {
                progress.CompletedAt = DateTime.UtcNow;
            }

            _dbContext.TaskProgress.Add(progress);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task progress for task {taskId}");
            return false;
        }
    }

    public async Task<List<TaskItem>> GetTasksAwaitingApprovalAsync()
    {
        return await _dbContext.Tasks
            .Include(t => t.ProgressHistory)
            .Where(t => t.LocalStatus == TaskStatus.AwaitingApproval)
            .OrderBy(t => t.CreatedDate)
            .ToListAsync();
    }

    public async Task<bool> ApproveTaskAsync(int taskId)
    {
        try
        {
            var task = await GetTaskAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning($"Task {taskId} not found");
                return false;
            }

            task.LocalStatus = TaskStatus.Approved;
            await UpdateTaskProgressAsync(taskId, ProgressType.HumanReview,
                ProgressStatus.Approved, "Task approved by user");

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Task {taskId} approved");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error approving task {taskId}");
            return false;
        }
    }

    public async Task<bool> RejectTaskAsync(int taskId, string reason)
    {
        try
        {
            var task = await GetTaskAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning($"Task {taskId} not found");
                return false;
            }

            task.LocalStatus = TaskStatus.Rejected;
            task.Notes = reason;

            await UpdateTaskProgressAsync(taskId, ProgressType.HumanReview,
                ProgressStatus.Rejected, $"Task rejected: {reason}");

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Task {taskId} rejected: {reason}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error rejecting task {taskId}");
            return false;
        }
    }

    private async Task ProcessNewTasksAsync()
    {
        var newTasks = await _dbContext.Tasks
            .Where(t => t.LocalStatus == TaskStatus.New)
            .ToListAsync();

        foreach (var task in newTasks)
        {
            try
            {
                // Try to find a suitable repository
                var repository = await _repositoryService.FindSuitableRepositoryAsync(task);

                if (repository != null)
                {
                    task.RepositoryUrl = repository.Url;
                    task.LocalStatus = TaskStatus.InProgress;

                    await UpdateTaskProgressAsync(task.Id, ProgressType.RepositoryAssignment,
                        ProgressStatus.Completed, $"Auto-assigned to repository: {repository.Name}");
                }
                else
                {
                    task.LocalStatus = TaskStatus.AwaitingApproval;
                    task.RequiresHumanApproval = true;

                    await UpdateTaskProgressAsync(task.Id, ProgressType.RepositoryAssignment,
                        ProgressStatus.AwaitingApproval, "No suitable repository found, requires human intervention");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing new task {task.Id}");
                await UpdateTaskProgressAsync(task.Id, ProgressType.RepositoryAssignment,
                    ProgressStatus.Failed, $"Error: {ex.Message}");
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    private static string GenerateBranchName(TaskItem task)
    {
        // Generate a branch name based on the Jira key and title
        var titlePart = task.Title
            .ToLower()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Replace("/", "-")
            .Replace("\\", "-");

        // Remove special characters and limit length
        titlePart = new string(titlePart.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        if (titlePart.Length > 30)
            titlePart = titlePart.Substring(0, 30).TrimEnd('-');

        return $"feature/{task.JiraKey.ToLower()}-{titlePart}";
    }
}
