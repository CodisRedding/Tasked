using Microsoft.Extensions.Logging;
using Spectre.Console;
using Tasked.Models;
using Tasked.Services;
using TaskStatus = Tasked.Models.TaskStatus;

namespace Tasked.UI;

public class ConsoleInterface
{
    private readonly ITaskManagementService _taskManager;
    private readonly ILogger<ConsoleInterface> _logger;

    public ConsoleInterface(ITaskManagementService taskManager, ILogger<ConsoleInterface> logger)
    {
        _taskManager = taskManager;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        AnsiConsole.Write(
            new FigletText("Tasked")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.WriteLine("🚀 Automated Task Management System");
        AnsiConsole.WriteLine();

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "📋 View Active Tasks",
                        "🔄 Sync from Jira",
                        "⏳ Review Pending Approvals",
                        "📊 Show Task Statistics",
                        "⚙️ Settings",
                        "❌ Exit"
                    }));

            try
            {
                switch (choice)
                {
                    case "📋 View Active Tasks":
                        await ShowActiveTasksAsync();
                        break;
                    case "🔄 Sync from Jira":
                        await SyncFromJiraAsync();
                        break;
                    case "⏳ Review Pending Approvals":
                        await ReviewPendingApprovalsAsync();
                        break;
                    case "📊 Show Task Statistics":
                        await ShowTaskStatisticsAsync();
                        break;
                    case "⚙️ Settings":
                        ShowSettings();
                        break;
                    case "❌ Exit":
                        AnsiConsole.WriteLine("👋 Goodbye!");
                        return;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                _logger.LogError(ex, "Error in console interface");
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }

            AnsiConsole.WriteLine();
        }
    }

    private async Task ShowActiveTasksAsync()
    {
        await AnsiConsole.Status()
            .StartAsync("Loading active tasks...", async ctx =>
            {
                var tasks = await _taskManager.GetActiveTasksAsync();

                if (!tasks.Any())
                {
                    AnsiConsole.WriteLine("📭 No active tasks found.");
                    return;
                }

                var table = new Table()
                    .BorderColor(Color.Grey)
                    .Border(TableBorder.Rounded)
                    .AddColumn("Jira Key")
                    .AddColumn("Title")
                    .AddColumn("Status")
                    .AddColumn("Priority")
                    .AddColumn("Repository")
                    .AddColumn("Branch");

                foreach (var task in tasks)
                {
                    var statusColor = GetStatusColor(task.LocalStatus);
                    var priorityColor = GetPriorityColor(task.Priority);

                    table.AddRow(
                        $"[bold]{task.JiraKey}[/]",
                        task.Title.Length > 40 ? task.Title.Substring(0, 37) + "..." : task.Title,
                        $"[{statusColor}]{task.LocalStatus}[/]",
                        $"[{priorityColor}]{task.Priority}[/]",
                        GetRepositoryName(task.RepositoryUrl) ?? "Not assigned",
                        task.BranchName ?? "No branch"
                    );
                }

                AnsiConsole.Write(table);
            });

        // Allow user to select a task for details
        var tasks = await _taskManager.GetActiveTasksAsync();
        if (tasks.Any())
        {
            var taskChoices = tasks.Select(t => $"{t.JiraKey} - {t.Title}").ToList();
            taskChoices.Add("🔙 Back to main menu");

            var selectedTask = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a task to view details (or go back):")
                    .PageSize(10)
                    .AddChoices(taskChoices));

            if (selectedTask != "🔙 Back to main menu")
            {
                var jiraKey = selectedTask.Split(" - ")[0];
                var task = tasks.First(t => t.JiraKey == jiraKey);
                await ShowTaskDetailsAsync(task);
            }
        }
    }

    private async Task ShowTaskDetailsAsync(TaskItem task)
    {
        var panel = new Panel(
            new Markup($"""
                [bold]Jira Key:[/] {task.JiraKey}
                [bold]Title:[/] {task.Title}
                [bold]Status:[/] [{GetStatusColor(task.LocalStatus)}]{task.LocalStatus}[/]
                [bold]Priority:[/] [{GetPriorityColor(task.Priority)}]{task.Priority}[/]
                [bold]Assignee:[/] {task.Assignee}
                [bold]Created:[/] {task.CreatedDate:yyyy-MM-dd HH:mm}
                [bold]Repository:[/] {GetRepositoryName(task.RepositoryUrl) ?? "Not assigned"}
                [bold]Branch:[/] {task.BranchName ?? "No branch"}

                [bold]Description:[/]
                {task.Description}

                [bold]Notes:[/]
                {task.Notes ?? "No notes"}
                """))
            .Header($"📋 Task Details: {task.JiraKey}")
            .BorderColor(Color.Blue);

        AnsiConsole.Write(panel);

        // Show progress history
        if (task.ProgressHistory.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("[bold]📈 Progress History:[/]");

            var progressTable = new Table()
                .BorderColor(Color.Grey)
                .Border(TableBorder.Rounded)
                .AddColumn("Date")
                .AddColumn("Action")
                .AddColumn("Status")
                .AddColumn("Description");

            foreach (var progress in task.ProgressHistory.OrderBy(p => p.CreatedAt))
            {
                var statusColor = GetProgressStatusColor(progress.Status);
                progressTable.AddRow(
                    progress.CreatedAt.ToString("MM-dd HH:mm"),
                    progress.Type.ToString(),
                    $"[{statusColor}]{progress.Status}[/]",
                    progress.Description
                );
            }

            AnsiConsole.Write(progressTable);
        }

        // Task actions
        var actions = new List<string> { "🔙 Back to task list" };

        if (task.LocalStatus == TaskStatus.AwaitingApproval)
        {
            actions.InsertRange(0, new[] { "✅ Approve", "❌ Reject" });
        }

        if (!string.IsNullOrEmpty(task.RepositoryUrl) && string.IsNullOrEmpty(task.BranchName))
        {
            actions.Insert(0, "🌿 Create Branch");
        }

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices(actions));

        switch (action)
        {
            case "✅ Approve":
                await _taskManager.ApproveTaskAsync(task.Id);
                AnsiConsole.WriteLine($"✅ Task {task.JiraKey} approved!");
                break;
            case "❌ Reject":
                var reason = AnsiConsole.Ask<string>("Why are you rejecting this task?");
                await _taskManager.RejectTaskAsync(task.Id, reason);
                AnsiConsole.WriteLine($"❌ Task {task.JiraKey} rejected.");
                break;
            case "🌿 Create Branch":
                var branchName = AnsiConsole.Ask<string>($"Branch name (press Enter for auto-generated):", string.Empty);
                await _taskManager.CreateBranchForTaskAsync(task.Id, string.IsNullOrEmpty(branchName) ? null : branchName);
                AnsiConsole.WriteLine($"🌿 Branch creation initiated for task {task.JiraKey}!");
                break;
        }
    }

    private async Task SyncFromJiraAsync()
    {
        await AnsiConsole.Status()
            .StartAsync("🔄 Syncing tasks from Jira...", async ctx =>
            {
                await _taskManager.SyncTasksFromJiraAsync();
            });

        AnsiConsole.WriteLine("✅ Jira sync completed!");
        AnsiConsole.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    private async Task ReviewPendingApprovalsAsync()
    {
        var pendingTasks = await _taskManager.GetTasksAwaitingApprovalAsync();

        if (!pendingTasks.Any())
        {
            AnsiConsole.WriteLine("✅ No tasks awaiting approval.");
            AnsiConsole.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return;
        }

        AnsiConsole.WriteLine($"⏳ Found {pendingTasks.Count} task(s) awaiting approval:");

        foreach (var task in pendingTasks)
        {
            AnsiConsole.WriteLine();
            await ShowTaskDetailsAsync(task);
        }
    }

    private async Task ShowTaskStatisticsAsync()
    {
        var activeTasks = await _taskManager.GetActiveTasksAsync();
        var pendingTasks = await _taskManager.GetTasksAwaitingApprovalAsync();

        var statusGroups = activeTasks.GroupBy(t => t.LocalStatus).ToList();

        var chart = new BreakdownChart()
            .Width(80)
            .ShowPercentage()
            .AddItem("New", statusGroups.FirstOrDefault(g => g.Key == TaskStatus.New)?.Count() ?? 0, Color.Yellow)
            .AddItem("In Progress", statusGroups.FirstOrDefault(g => g.Key == TaskStatus.InProgress)?.Count() ?? 0, Color.Blue)
            .AddItem("Awaiting Approval", statusGroups.FirstOrDefault(g => g.Key == TaskStatus.AwaitingApproval)?.Count() ?? 0, Color.Orange1)
            .AddItem("Approved", statusGroups.FirstOrDefault(g => g.Key == TaskStatus.Approved)?.Count() ?? 0, Color.Green)
            .AddItem("Rejected", statusGroups.FirstOrDefault(g => g.Key == TaskStatus.Rejected)?.Count() ?? 0, Color.Red)
            .AddItem("Blocked", statusGroups.FirstOrDefault(g => g.Key == TaskStatus.Blocked)?.Count() ?? 0, Color.DarkRed);

        AnsiConsole.Write(
            new Panel(chart)
                .Header("📊 Task Statistics")
                .BorderColor(Color.Green));

        AnsiConsole.WriteLine($"\n📋 Total Active Tasks: {activeTasks.Count}");
        AnsiConsole.WriteLine($"⏳ Pending Approvals: {pendingTasks.Count}");

        AnsiConsole.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }

    private static void ShowSettings()
    {
        AnsiConsole.WriteLine("⚙️ Settings");
        AnsiConsole.WriteLine("Configuration file: appsettings.json");
        AnsiConsole.WriteLine("Database: tasked.db");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Edit the appsettings.json file to configure:");
        AnsiConsole.WriteLine("- Jira connection details");
        AnsiConsole.WriteLine("- Repository providers (BitBucket, GitLab)");
        AnsiConsole.WriteLine("- Workflow settings");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    private static string GetStatusColor(TaskStatus status) => status switch
    {
        TaskStatus.New => "yellow",
        TaskStatus.InProgress => "blue",
        TaskStatus.AwaitingApproval => "orange1",
        TaskStatus.Approved => "green",
        TaskStatus.Rejected => "red",
        TaskStatus.Completed => "green",
        TaskStatus.Blocked => "darkred",
        _ => "white"
    };

    private static string GetPriorityColor(string priority) => priority?.ToLower() switch
    {
        "highest" => "red",
        "high" => "orange1",
        "medium" => "yellow",
        "low" => "blue",
        "lowest" => "grey",
        _ => "white"
    };

    private static string GetProgressStatusColor(ProgressStatus status) => status switch
    {
        ProgressStatus.Pending => "yellow",
        ProgressStatus.InProgress => "blue",
        ProgressStatus.Completed => "green",
        ProgressStatus.Failed => "red",
        ProgressStatus.AwaitingApproval => "orange1",
        ProgressStatus.Approved => "green",
        ProgressStatus.Rejected => "red",
        _ => "white"
    };

    private static string? GetRepositoryName(string? repositoryUrl)
    {
        if (string.IsNullOrEmpty(repositoryUrl))
            return null;

        // Extract repository name from URL
        var uri = new Uri(repositoryUrl);
        var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return pathParts.Length > 0 ? pathParts.Last().Replace(".git", "") : null;
    }
}
