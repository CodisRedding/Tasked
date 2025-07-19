using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tasked.Configuration;
using Tasked.Models;

namespace Tasked.Services;

public interface IJiraService
{
    Task<List<TaskItem>> GetTasksAsync();
    Task<TaskItem?> GetTaskAsync(string jiraKey);
    Task<bool> UpdateTaskStatusAsync(string jiraKey, string status, string? comment = null);
    Task<bool> AddCommentAsync(string jiraKey, string comment);
}

public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraConfiguration _config;
    private readonly ILogger<JiraService> _logger;

    public JiraService(HttpClient httpClient, JiraConfiguration config, ILogger<JiraService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);

        // Basic authentication with username and API token
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_config.Username}:{_config.ApiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<TaskItem>> GetTasksAsync()
    {
        try
        {
            var jql = BuildJqlQuery();
            var encodedJql = Uri.EscapeDataString(jql);
            var url = $"/rest/api/3/search?jql={encodedJql}&maxResults=100&fields=summary,description,status,priority,assignee,reporter,created,updated,duedate";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var tasks = new List<TaskItem>();
            if (root.TryGetProperty("issues", out var issuesElement))
            {
                foreach (var issue in issuesElement.EnumerateArray())
                {
                    var task = ConvertJiraIssueToTask(issue);
                    if (task != null)
                        tasks.Add(task);
                }
            }

            _logger.LogInformation($"Retrieved {tasks.Count} tasks from Jira");
            return tasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tasks from Jira");
            return new List<TaskItem>();
        }
    }

    public async Task<TaskItem?> GetTaskAsync(string jiraKey)
    {
        try
        {
            var url = $"/rest/api/3/issue/{jiraKey}?fields=summary,description,status,priority,assignee,reporter,created,updated,duedate";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Task {jiraKey} not found in Jira");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var issue = document.RootElement;
            return ConvertJiraIssueToTask(issue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving task {jiraKey} from Jira");
            return null;
        }
    }

    public async Task<bool> UpdateTaskStatusAsync(string jiraKey, string status, string? comment = null)
    {
        try
        {
            // First, get available transitions
            var transitionsUrl = $"/rest/api/3/issue/{jiraKey}/transitions";
            var transitionsResponse = await _httpClient.GetAsync(transitionsUrl);
            transitionsResponse.EnsureSuccessStatusCode();

            var transitionsContent = await transitionsResponse.Content.ReadAsStringAsync();
            using var transitionsDocument = JsonDocument.Parse(transitionsContent);
            var transitionsRoot = transitionsDocument.RootElement;

            JsonElement? targetTransition = null;
            if (transitionsRoot.TryGetProperty("transitions", out var transitionsElement))
            {
                foreach (var transition in transitionsElement.EnumerateArray())
                {
                    if (transition.TryGetProperty("to", out var toElement) &&
                        toElement.TryGetProperty("name", out var nameElement) &&
                        nameElement.GetString()?.Equals(status, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        targetTransition = transition;
                        break;
                    }
                }
            }

            if (targetTransition == null)
            {
                _logger.LogWarning($"No transition found to status '{status}' for task {jiraKey}");
                return false;
            }

            // Perform the transition
            var transitionId = targetTransition.Value.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var transitionRequest = new
            {
                transition = new { id = transitionId }
            };

            var json = JsonSerializer.Serialize(transitionRequest);
            var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await _httpClient.PostAsync($"/rest/api/3/issue/{jiraKey}/transitions", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully updated task {jiraKey} status to {status}");

                // Add comment if provided
                if (!string.IsNullOrEmpty(comment))
                {
                    await AddCommentAsync(jiraKey, comment);
                }

                return true;
            }
            else
            {
                _logger.LogError($"Failed to update task {jiraKey} status. Response: {await response.Content.ReadAsStringAsync()}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {jiraKey} status");
            return false;
        }
    }

    public async Task<bool> AddCommentAsync(string jiraKey, string comment)
    {
        try
        {
            var commentRequest = new
            {
                body = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = comment
                                }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(commentRequest);
            var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var response = await _httpClient.PostAsync($"/rest/api/3/issue/{jiraKey}/comment", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully added comment to task {jiraKey}");
                return true;
            }
            else
            {
                _logger.LogError($"Failed to add comment to task {jiraKey}. Response: {await response.Content.ReadAsStringAsync()}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding comment to task {jiraKey}");
            return false;
        }
    }

    private string BuildJqlQuery()
    {
        // Use the configured JQL query directly
        var jql = _config.JqlQuery;

        // Add ordering if not already present
        if (!jql.ToLower().Contains("order by"))
        {
            jql += " ORDER BY created DESC";
        }

        return jql;
    }

    private TaskItem? ConvertJiraIssueToTask(JsonElement issue)
    {
        try
        {
            if (!issue.TryGetProperty("fields", out var fields))
                return null;

            var task = new TaskItem
            {
                JiraKey = issue.TryGetProperty("key", out var keyElement) ? keyElement.GetString() ?? string.Empty : string.Empty,
                Title = fields.TryGetProperty("summary", out var summaryElement) ? summaryElement.GetString() ?? string.Empty : string.Empty,
                Description = fields.TryGetProperty("description", out var descElement) ? descElement.GetString() ?? string.Empty : string.Empty,
                Status = GetNestedStringValue(fields, "status", "name"),
                Priority = GetNestedStringValue(fields, "priority", "name"),
                Assignee = GetNestedStringValue(fields, "assignee", "displayName"),
                Reporter = GetNestedStringValue(fields, "reporter", "displayName"),
                LastSyncedAt = DateTime.UtcNow
            };

            if (fields.TryGetProperty("created", out var createdElement) &&
                DateTime.TryParse(createdElement.GetString(), out var created))
                task.CreatedDate = created;

            if (fields.TryGetProperty("updated", out var updatedElement) &&
                DateTime.TryParse(updatedElement.GetString(), out var updated))
                task.UpdatedDate = updated;

            if (fields.TryGetProperty("duedate", out var dueDateElement) &&
                DateTime.TryParse(dueDateElement.GetString(), out var dueDate))
                task.DueDate = dueDate;

            return task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting Jira issue to TaskItem");
            return null;
        }
    }

    private static string GetNestedStringValue(JsonElement parent, string property1, string property2)
    {
        if (parent.TryGetProperty(property1, out var level1Element) &&
            level1Element.TryGetProperty(property2, out var level2Element))
        {
            return level2Element.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}
