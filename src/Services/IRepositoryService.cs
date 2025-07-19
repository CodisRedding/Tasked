using Microsoft.Extensions.Logging;
using Tasked.Models;

namespace Tasked.Services;

public interface IRepositoryService
{
    Task<List<Repository>> GetAvailableRepositoriesAsync();
    Task<Repository?> CreateRepositoryAsync(string name, string description, RepositoryProvider provider);
    Task<Repository?> FindSuitableRepositoryAsync(TaskItem task);
    Task<bool> CreateBranchAsync(Repository repository, string branchName, string? baseBranch = null);
    Task<bool> RepositoryExistsAsync(string name, RepositoryProvider provider);
    Task<string?> GetDefaultBranchAsync(Repository repository);
}

public abstract class BaseRepositoryService : IRepositoryService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    protected BaseRepositoryService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public abstract Task<List<Repository>> GetAvailableRepositoriesAsync();
    public abstract Task<Repository?> CreateRepositoryAsync(string name, string description, RepositoryProvider provider);
    public abstract Task<bool> CreateBranchAsync(Repository repository, string branchName, string? baseBranch = null);
    public abstract Task<bool> RepositoryExistsAsync(string name, RepositoryProvider provider);
    public abstract Task<string?> GetDefaultBranchAsync(Repository repository);

    public virtual async Task<Repository?> FindSuitableRepositoryAsync(TaskItem task)
    {
        // Default implementation: try to find a repository based on task properties
        var repositories = await GetAvailableRepositoriesAsync();

        // Simple heuristic: look for repositories with similar names or keywords
        var taskKeywords = ExtractKeywordsFromTask(task);

        var matchingRepo = repositories
            .Where(r => r.IsActive)
            .OrderByDescending(r => CalculateRepoMatch(r, taskKeywords))
            .FirstOrDefault();

        return matchingRepo;
    }

    protected virtual List<string> ExtractKeywordsFromTask(TaskItem task)
    {
        var keywords = new List<string>();

        // Extract from title and description
        var text = $"{task.Title} {task.Description}".ToLower();

        // Simple keyword extraction (could be enhanced with NLP)
        var commonWords = new[] { "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "is", "are", "was", "were", "be", "been", "have", "has", "had", "do", "does", "did", "will", "would", "should", "could", "can", "may", "might", "must", "shall" };

        keywords.AddRange(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3 && !commonWords.Contains(word))
            .Select(word => word.Trim('.', ',', '!', '?', ';', ':')));

        return keywords.Distinct().ToList();
    }

    protected virtual double CalculateRepoMatch(Repository repository, List<string> keywords)
    {
        var repoText = $"{repository.Name} {repository.Description}".ToLower();
        var matches = keywords.Count(keyword => repoText.Contains(keyword));
        return (double)matches / Math.Max(keywords.Count, 1);
    }
}
