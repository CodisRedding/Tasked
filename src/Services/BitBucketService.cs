using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tasked.Configuration;
using Tasked.Models;

namespace Tasked.Services;

public class BitBucketService : BaseRepositoryService
{
    private readonly RepositoryProviderConfiguration _config;
    private new readonly ILogger<BitBucketService> _logger;

    public BitBucketService(HttpClient httpClient, RepositoryProviderConfiguration config, ILogger<BitBucketService> logger)
        : base(httpClient, logger)
    {
        _config = config;
        _logger = logger;
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/'));

        // Use Basic Authentication with username and app password for BitBucket
        // After Sept 9, 2025, this will transition to API tokens
        var token = !string.IsNullOrEmpty(_config.AppPassword) ? _config.AppPassword :
                    !string.IsNullOrEmpty(_config.ApiToken) ? _config.ApiToken :
                    _config.AccessToken; // Legacy fallback

        if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(token))
        {
            var authValue = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_config.Username}:{token}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public override async Task<List<Repository>> GetAvailableRepositoriesAsync()
    {
        try
        {
            var url = $"/2.0/repositories/{_config.WorkspaceOrOrganization}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var result = new List<Repository>();
            if (root.TryGetProperty("values", out var valuesElement))
            {
                foreach (var repo in valuesElement.EnumerateArray())
                {
                    var repository = new Repository
                    {
                        Name = repo.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                        Url = GetCloneUrl(repo),
                        Description = repo.TryGetProperty("description", out var descElement) ? descElement.GetString() : null,
                        Provider = RepositoryProvider.BitBucket,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        DefaultBranch = GetMainBranch(repo)
                    };
                    result.Add(repository);
                }
            }

            _logger.LogInformation($"Retrieved {result.Count} repositories from BitBucket");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving repositories from BitBucket");
            return new List<Repository>();
        }
    }

    public override async Task<Repository?> CreateRepositoryAsync(string name, string description, RepositoryProvider provider)
    {
        try
        {
            var repositoryData = new
            {
                name = name,
                description = description,
                is_private = true,
                scm = "git",
                project = new
                {
                    key = _config.AdditionalSettings.GetValueOrDefault("ProjectKey", "")
                }
            };

            var json = JsonSerializer.Serialize(repositoryData);
            var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var url = $"/2.0/repositories/{_config.WorkspaceOrOrganization}/{name}";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var repository = new Repository
                {
                    Name = name,
                    Url = GetCloneUrl(root),
                    Description = description,
                    Provider = RepositoryProvider.BitBucket,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    DefaultBranch = "main"
                };

                _logger.LogInformation($"Successfully created BitBucket repository: {name}");
                return repository;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to create BitBucket repository {name}. Response: {errorContent}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating BitBucket repository: {name}");
            return null;
        }
    }

    public override async Task<bool> CreateBranchAsync(Repository repository, string branchName, string? baseBranch = null)
    {
        try
        {
            baseBranch ??= repository.DefaultBranch ?? "main";

            // First, get the commit hash of the base branch
            var baseCommitUrl = $"/2.0/repositories/{_config.WorkspaceOrOrganization}/{repository.Name}/refs/branches/{baseBranch}";
            var baseCommitResponse = await _httpClient.GetAsync(baseCommitUrl);
            baseCommitResponse.EnsureSuccessStatusCode();

            var baseCommitContent = await baseCommitResponse.Content.ReadAsStringAsync();
            using var baseCommitDocument = JsonDocument.Parse(baseCommitContent);
            var baseCommitRoot = baseCommitDocument.RootElement;

            var commitHash = baseCommitRoot.TryGetProperty("target", out var targetElement) &&
                           targetElement.TryGetProperty("hash", out var hashElement)
                           ? hashElement.GetString() : null;

            if (string.IsNullOrEmpty(commitHash))
            {
                _logger.LogError($"Could not get commit hash for base branch {baseBranch}");
                return false;
            }

            // Create the new branch
            var branchData = new
            {
                name = branchName,
                target = new
                {
                    hash = commitHash
                }
            };

            var json = JsonSerializer.Serialize(branchData);
            var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            var url = $"/2.0/repositories/{_config.WorkspaceOrOrganization}/{repository.Name}/refs/branches";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Successfully created branch {branchName} in repository {repository.Name}");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to create branch {branchName}. Response: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating branch {branchName} in repository {repository.Name}");
            return false;
        }
    }

    public override async Task<bool> RepositoryExistsAsync(string name, RepositoryProvider provider)
    {
        try
        {
            var url = $"/2.0/repositories/{_config.WorkspaceOrOrganization}/{name}";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking if repository {name} exists");
            return false;
        }
    }

    public override async Task<string?> GetDefaultBranchAsync(Repository repository)
    {
        try
        {
            var url = $"/2.0/repositories/{_config.WorkspaceOrOrganization}/{repository.Name}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            return GetMainBranch(root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting default branch for repository {repository.Name}");
            return "main";
        }
    }

    private static string GetCloneUrl(JsonElement repoElement)
    {
        if (repoElement.TryGetProperty("links", out var linksElement) &&
            linksElement.TryGetProperty("clone", out var cloneElement))
        {
            foreach (var clone in cloneElement.EnumerateArray())
            {
                if (clone.TryGetProperty("href", out var hrefElement))
                {
                    return hrefElement.GetString() ?? string.Empty;
                }
            }
        }
        return string.Empty;
    }

    private static string GetMainBranch(JsonElement repoElement)
    {
        if (repoElement.TryGetProperty("mainbranch", out var mainbranchElement) &&
            mainbranchElement.TryGetProperty("name", out var nameElement))
        {
            return nameElement.GetString() ?? "main";
        }
        return "main";
    }
}
