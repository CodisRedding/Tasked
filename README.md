# Tasked - Automated Task Management System

**Tasked** is an automated task management system that bridges Jira with source control platforms (BitBucket, GitLab) and tracks progress locally using SQLite. It helps developers automate the workflow from task assignment to repository management.

## 🚀 Features

- **Jira Integration**: Sync tasks from Jira automatically
- **Repository Management**: Connect with BitBucket and GitLab (GitHub and Azure DevOps support planned)
- **Local Progress Tracking**: SQLite database tracks all activities and progress
- **Human Approval Workflow**: Configurable approval processes for automated actions
- **Branch Management**: Automatically create feature branches based on Jira tasks
- **Cross-Platform**: Works on Windows, Linux, and macOS
- **Rich Console Interface**: Beautiful CLI using Spectre.Console

## 🏗️ Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│      Jira       │    │   Source Control │    │  Local SQLite   │
│   (Tasks)       │◄──►│  (BitBucket/    │◄──►│   (Progress)    │
│                 │    │   GitLab)       │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         ▲                        ▲                        ▲
         └────────────────────────┼────────────────────────┘
                                  ▼
                    ┌─────────────────────────┐
                    │     Tasked Engine       │
                    │  (Task Management)      │
                    └─────────────────────────┘
                                  ▲
                                  ▼
                    ┌─────────────────────────┐
                    │   Console Interface     │
                    │  (Human Interaction)    │
                    └─────────────────────────┘
```

## 📋 Current Status

This project is in active development. The basic structure and core services have been implemented:

### ✅ Completed
- Project structure and configuration
- Core data models (Tasks, Progress, Repositories)
- SQLite database with Entity Framework Core
- Jira service integration
- BitBucket service implementation
- Task management service
- Console interface with Spectre.Console
- Configuration management
- .NET SDK compilation issues resolved

### 🚧 In Progress
- Testing and debugging
- Configuration setup documentation

### 📅 Planned Features
- GitLab service implementation
- GitHub integration
- Azure DevOps integration
- Web interface (optional)
- Docker containerization
- CI/CD integration
- Advanced task assignment algorithms
- Notification system

## 🛠️ Setup

Tasked includes an **interactive setup wizard** that guides you through the entire configuration process:

```bash
# Quick setup with guided wizard
dotnet run --setup
```

The wizard will help you:

- Configure Jira API tokens with step-by-step instructions
- Set up repository connections (BitBucket, GitLab, GitHub)
- Configure workflow preferences
- Test your connections

## 🔐 Token Health Monitoring

Check the health of your API tokens at any time:

```bash
# Check all configured API tokens
dotnet run --token-health
```

This will verify:
- **Jira API Token**: Authentication and account status
- **Repository Tokens**: Access permissions and validity
- **Connection Issues**: Network and configuration problems

The health check will alert you when tokens need to be renewed or recreated.

## 🔄 Token Replacement

When tokens expire or need updating, use the interactive token replacement wizard:

```bash
# Replace expired or compromised tokens
dotnet run --update-token
```

This wizard will:
- Show current token status
- Let you select which token to replace
- Guide you through creating new tokens
- Test the updated tokens automatically
- Preserve all other configuration settings

**Perfect for:** Token rotation, security incidents, or when tokens stop working.

### Manual Setup (Alternative)

### Prerequisites

- .NET 8.0 or later
- Git

### Configuration

1. **Copy and edit the configuration file:**
   ```bash
   cp appsettings.json appsettings.local.json
   ```

2. **Configure Jira connection** in `appsettings.json`:
   ```json
   {
     "Jira": {
       "BaseUrl": "https://your-domain.atlassian.net",
       "Username": "your-email@domain.com",
       "ApiToken": "your-api-token",
       "Project": "YOUR_PROJECT_KEY"
     }
   }
   ```

3. **Configure Repository Providers:**
   ```json
   {
     "RepositoryProviders": [
       {
         "Name": "BitBucket",
         "BaseUrl": "https://api.bitbucket.org",
         "AppPassword": "your-bitbucket-app-password",
         "Username": "your-bitbucket-username",
         "WorkspaceOrOrganization": "your-workspace",
         "IsDefault": true
       }
     ]
   }
   ```

   > **Note:** BitBucket App Passwords will transition to API Tokens on September 9, 2025. After this date, replace `AppPassword` with `ApiToken` and use your Atlassian API token instead.
   ```

### Running the Application

```bash
# Restore packages
dotnet restore

# Run the application
dotnet run
```

## 🎯 Usage Scenarios

### Scenario 1: Automated Task Processing
1. **Sync from Jira** - Fetch new tasks from your Jira project
2. **Auto-assign Repositories** - System finds suitable repositories based on task content
3. **Create Branches** - Automatically create feature branches with standardized naming
4. **Track Progress** - Monitor all activities in the local database

### Scenario 2: Human-in-the-Loop Workflow
1. **Review Tasks** - View tasks requiring human approval
2. **Manual Repository Assignment** - Choose specific repositories for tasks
3. **Approve/Reject Actions** - Review and approve automated suggestions
4. **Monitor Progress** - Track completion status and history

## 📊 Console Interface

The application provides a rich console interface with:

- **📋 View Active Tasks** - See all current tasks with status
- **🔄 Sync from Jira** - Manually trigger Jira synchronization
- **⏳ Review Pending Approvals** - Handle tasks awaiting human review
- **📊 Show Task Statistics** - Visual breakdown of task statuses
- **⚙️ Settings** - Configuration management

## 🔧 Configuration Options

### Workflow Settings
```json
{
  "Workflow": {
    "RequireHumanApproval": true,      // Require approval for automated actions
    "AutoCreateRepositories": false,   // Automatically create new repositories
    "AutoCreateBranches": true,        // Auto-create branches for tasks
    "AutoUpdateJira": true,            // Update Jira with progress
    "MaxConcurrentTasks": 3            // Maximum tasks to process simultaneously
  }
}
```

### Database Configuration
```json
{
  "Database": {
    "ConnectionString": "Data Source=tasked.db"
  }
}
```

## 🤝 Contributing

This project welcomes contributions! Areas where help is needed:

1. **Repository Providers** - GitLab, GitHub, Azure DevOps implementations
2. **Testing** - Unit tests and integration tests
3. **Documentation** - API documentation and user guides
4. **Features** - New functionality and improvements

## 📝 Branch Naming Convention

Tasked automatically generates branch names using the pattern:
```
feature/{jira-key}-{sanitized-title}
```

Example: `feature/proj-123-implement-user-authentication`

## 🗄️ Database Schema

The SQLite database tracks:
- **Tasks** - Jira task information and local status
- **TaskProgress** - Detailed progress history for each task
- **Repositories** - Configured source control repositories

## 🔒 Security Notes

- Store sensitive configuration (API tokens) in environment variables or secure configuration files
- Use app passwords or API tokens, not user passwords
- Consider using Azure Key Vault or similar for production deployments

## 📚 Dependencies

- **Microsoft.EntityFrameworkCore.Sqlite** - Database ORM
- **Microsoft.Extensions.*** - Configuration and dependency injection
- **Spectre.Console** - Rich console interface
- **System.Text.Json** - JSON serialization

## 🐛 Known Issues

1. **Error Handling** - Some error scenarios need more robust handling
2. **Performance** - Large Jira projects may need pagination improvements

## 📚 Documentation

For detailed documentation, see the `docs/` directory:

- **[Quick Reference](QUICK_REF.md)** - Essential commands and troubleshooting
- **[Developer Setup](docs/DEV_SETUP.md)** - Complete developer onboarding guide
- **[Development Log](docs/DEVELOPMENT.md)** - Recent features and development history
- **[Architecture Decisions](docs/ADR.md)** - Technical decision records and rationale
- **[Setup Guide](docs/TODO.md)** - Step-by-step setup instructions with credential configuration
- **[Configuration Reference](docs/CONFIGURATION.md)** - Complete configuration options and examples  
- **[SDK Troubleshooting](docs/SDK_FIX_DOCUMENTATION.md)** - .NET SDK resolution issues and fixes
- **[Resolution Summary](docs/RESOLUTION_SUMMARY.md)** - Summary of resolved technical issues

## 📄 License

This project is open source. License details to be determined.

---

**Note**: This is an active development project. The codebase is functional but needs testing and debugging to ensure full compatibility across different environments.
