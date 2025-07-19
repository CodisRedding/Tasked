using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tasked.Configuration;
using Tasked.Data;
using Tasked.Models;
using Tasked.Services;
using Tasked.UI;
using Tasked.Setup;

namespace Tasked;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Check for setup command
            if (args.Contains("--setup") || args.Contains("-s"))
            {
                var setupWizard = new SetupWizard();
                var success = await setupWizard.RunSetupAsync();
                Environment.Exit(success ? 0 : 1);
                return;
            }

            // Check for token health command
            if (args.Contains("--token-health") || args.Contains("-t"))
            {
                await RunTokenHealthCheckAsync();
                return;
            }

            // Check for token update command
            if (args.Contains("--update-token") || args.Contains("-u"))
            {
                var tokenUpdate = new TokenUpdateService();
                var success = await tokenUpdate.RunTokenUpdateAsync();
                Environment.Exit(success ? 0 : 1);
                return;
            }

            // Check if configuration exists, if not, prompt for setup
            if (!File.Exists("appsettings.local.json"))
            {
                Console.WriteLine("🔧 No configuration found. Let's set up Tasked!");
                Console.WriteLine("Run with --setup flag to configure Jira and repository connections.");
                Console.WriteLine();
                Console.WriteLine("Usage: dotnet run --setup");
                Environment.Exit(1);
                return;
            }

            var host = CreateHostBuilder(args).Build();

            // Ensure database is created
            using (var scope = host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TaskedDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
            }

            // Run the console interface
            var consoleInterface = host.Services.GetRequiredService<ConsoleInterface>();
            await consoleInterface.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static async Task RunTokenHealthCheckAsync()
    {
        try
        {
            // Create configuration - use proper paths relative to working directory
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();

            // Check if configured
            if (!File.Exists("appsettings.local.json"))
            {
                Console.WriteLine("❌ No configuration found. Run setup first:");
                Console.WriteLine("dotnet run --setup");
                return;
            }

            // Create services for token health check
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddHttpClient();
            services.AddSingleton<IConfiguration>(config);
            services.AddTransient<TokenHealthService>();

            var serviceProvider = services.BuildServiceProvider();
            var tokenHealthService = serviceProvider.GetRequiredService<TokenHealthService>();

            var report = await tokenHealthService.CheckAllTokensAsync();
            tokenHealthService.DisplayHealthReport(report);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error checking token health: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                var appConfig = new AppConfiguration();
                context.Configuration.Bind(appConfig);
                services.AddSingleton(appConfig);
                services.AddSingleton(appConfig.Jira);
                services.AddSingleton(appConfig.Database);
                services.AddSingleton(appConfig.Workflow);

                // Database
                services.AddDbContext<TaskedDbContext>(options =>
                    options.UseSqlite(appConfig.Database.ConnectionString));

                // HTTP Clients
                services.AddHttpClient<IJiraService, JiraService>();

                // Repository Services - Register the default provider
                var defaultProvider = appConfig.RepositoryProviders.FirstOrDefault(p => p.IsDefault);
                if (defaultProvider != null)
                {
                    services.AddSingleton(defaultProvider);

                    switch (defaultProvider.Name.ToLower())
                    {
                        case "bitbucket":
                            services.AddHttpClient<IRepositoryService, BitBucketService>();
                            break;
                        default:
                            throw new NotSupportedException($"Repository provider '{defaultProvider.Name}' is not supported yet");
                    }
                }
                else
                {
                    // Fallback to a mock implementation
                    services.AddTransient<IRepositoryService, MockRepositoryService>();
                }

                // Core Services
                services.AddTransient<ITaskManagementService, TaskManagementService>();

                // UI
                services.AddTransient<ConsoleInterface>();

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
}

// Mock repository service for when no providers are configured
public class MockRepositoryService : IRepositoryService
{
    private readonly ILogger<MockRepositoryService> _logger;

    public MockRepositoryService(ILogger<MockRepositoryService> logger)
    {
        _logger = logger;
    }

    public Task<List<Repository>> GetAvailableRepositoriesAsync()
    {
        _logger.LogWarning("Using mock repository service - no repository providers configured");
        return Task.FromResult(new List<Repository>());
    }

    public Task<Repository?> CreateRepositoryAsync(string name, string description, RepositoryProvider provider)
    {
        _logger.LogWarning("Mock repository service cannot create repositories");
        return Task.FromResult<Repository?>(null);
    }

    public Task<Repository?> FindSuitableRepositoryAsync(TaskItem task)
    {
        _logger.LogWarning("Mock repository service cannot find repositories");
        return Task.FromResult<Repository?>(null);
    }

    public Task<bool> CreateBranchAsync(Repository repository, string branchName, string? baseBranch = null)
    {
        _logger.LogWarning("Mock repository service cannot create branches");
        return Task.FromResult(false);
    }

    public Task<bool> RepositoryExistsAsync(string name, RepositoryProvider provider)
    {
        return Task.FromResult(false);
    }

    public Task<string?> GetDefaultBranchAsync(Repository repository)
    {
        return Task.FromResult<string?>("main");
    }
}
