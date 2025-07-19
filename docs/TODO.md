# 📋 Tasked Setup Guide

This guide shows you how to set up Tasked - the automated task management system that bridges Jira with your repositories.

## 🚀 Quick Start (Recommended)

**Interactive Setup Wizard** - *Time: 10-15 minutes*

```bash
dotnet run --setup
```

The setup wizard automates the entire configuration process with step-by-step guidance for:
- Jira API token creation with visual instructions
- Repository provider setup (BitBucket, GitLab, GitHub)
- Workflow preferences configuration
- Automatic connection testing

## 🔐 Token Health Monitoring

**Monitor API Token Status** - *Time: 30 seconds*

```bash
dotnet run --token-health
```

Regular token health checks help you:
- **Detect expiring tokens** before they break automation
- **Validate permissions** for all configured services
- **Troubleshoot connection issues** with detailed error reporting
- **Get renewal reminders** with direct links to token management

**Recommended**: Run token health checks weekly or when experiencing connection issues.

## 🔄 Token Replacement

**Replace Expired/Compromised Tokens** - *Time: 5-10 minutes*

```bash
dotnet run --update-token
```

Interactive token replacement when you need to:
- **Replace expired tokens** without losing other settings
- **Update compromised credentials** quickly and securely
- **Rotate tokens regularly** for security best practices
- **Fix authentication issues** with guided token recreation

**Features**: Status display, selective replacement, automatic testing, configuration preservation.

---

## 📖 Manual Setup (Alternative)

If you prefer to configure manually or need to understand the configuration structure:

## 🔑 Authentication & Credentials

### [ ] 1. Jira API Token
- [ ] Go to [Atlassian API Tokens](https://id.atlassian.com/manage-profile/security/api-tokens)
- [ ] Click "Create API token"
- [ ] Label: "Tasked Application"
- [ ] **Copy the token immediately** (you can't see it again!)
- [ ] Update `appsettings.json` → `Jira.ApiToken`

### [ ] 2. Jira Configuration Details
- [ ] Find your Jira domain (e.g., `https://mycompany.atlassian.net`)
- [ ] Update `appsettings.json` → `Jira.BaseUrl`
- [ ] Update `appsettings.json` → `Jira.Username` (your Atlassian email)
- [ ] Find your project key (2-5 letters, visible in Jira project URL)
- [ ] Update `appsettings.json` → `Jira.Project`

### [ ] 3. BitBucket App Password
- [ ] Go to [BitBucket App Passwords](https://bitbucket.org/account/settings/app-passwords/)
- [ ] Click "Create app password"
- [ ] Name: "Tasked Application"
- [ ] **Required Permissions:**
  - [ ] Repositories: Read ✅
  - [ ] Repositories: Write ✅
  - [ ] Pull requests: Read ✅
  - [ ] Pull requests: Write ✅
  - [ ] Issues: Read ✅ (optional)
- [ ] **Copy the password immediately** (you can't see it again!)
- [ ] Update `appsettings.json` → `RepositoryProviders[0].AppPassword`
- [ ] Update `appsettings.json` → `RepositoryProviders[0].Username`

> **Note**: After September 9, 2025, BitBucket will deprecate App Passwords in favor of API Tokens. Update your configuration to use `ApiToken` instead of `AppPassword` and authenticate with your Atlassian API token.

### [ ] 4. BitBucket Workspace Details
- [ ] Find your BitBucket workspace name (visible in URLs)
- [ ] Update `appsettings.json` → `RepositoryProviders[0].WorkspaceOrOrganization`
- [ ] If using BitBucket Projects, find your project key
- [ ] Update `appsettings.json` → `RepositoryProviders[0].AdditionalSettings.ProjectKey`

## ⚙️ Configuration Customization

### [ ] 5. Jira Status Filters
- [ ] Review your Jira workflow statuses
- [ ] Update `appsettings.json` → `Jira.StatusFilter` with relevant statuses:
  - Common options: "To Do", "In Progress", "Ready for Development", "Code Review"
- [ ] Adjust `SyncIntervalMinutes` if needed (default: 15 minutes)

### [ ] 6. Workflow Preferences
- [ ] **RequireHumanApproval**: 
  - [ ] `true` = Manual review of all automated actions (recommended for start)
  - [ ] `false` = Fully automated (use after testing)
- [ ] **AutoCreateRepositories**: 
  - [ ] `false` = Safer, assign to existing repos only (recommended)
  - [ ] `true` = Create new repositories automatically
- [ ] **AutoCreateBranches**: 
  - [ ] `true` = Automatically create feature branches (recommended)
  - [ ] `false` = Manual branch creation only
- [ ] **AutoUpdateJira**: 
  - [ ] `true` = Update Jira with progress (recommended)
  - [ ] `false` = No Jira updates
- [ ] **MaxConcurrentTasks**: Adjust based on your needs (3-5 recommended)

## 🛠️ Technical Setup

### [ ] 7. .NET SDK Issues Resolution
- [ ] **Try Method 1**: Download and reinstall .NET SDK from [official site](https://dotnet.microsoft.com/download)
- [ ] **Try Method 2**: Remove conflicting Homebrew installations
  ```bash
  brew list | grep dotnet
  brew uninstall --cask [any-dotnet-packages-found]
  ```
- [ ] **Try Method 3**: Use .NET uninstall tool for clean reinstall
- [ ] **Try Method 4**: Set environment variable workaround
  ```bash
  echo 'export MSBuildEnableWorkloadResolver=false' >> ~/.zshrc
  source ~/.zshrc
  ```
- [ ] **Test**: Run `dotnet restore` in the Tasked directory

### [ ] 8. Build and Test Application
- [ ] Navigate to Tasked directory: `cd /Users/rocky.assad/code/Tasked`
- [ ] Restore packages: `dotnet restore`
- [ ] Build project: `dotnet build`
- [ ] Run application: `dotnet run`

## 🧪 Initial Testing

### [ ] 9. Test Jira Connection
- [ ] Run the application
- [ ] Try "🔄 Sync from Jira" option
- [ ] Verify tasks are being fetched
- [ ] Check for any authentication errors

### [ ] 10. Test Repository Connection
- [ ] Ensure you have at least one repository in your BitBucket workspace
- [ ] Run task processing
- [ ] Verify repository assignment works
- [ ] Test branch creation (if enabled)

### [ ] 11. Test Workflow
- [ ] Process a simple task end-to-end
- [ ] Verify Jira comments are added
- [ ] Check branch naming convention: `feature/{jira-key}-{task-title}`
- [ ] Test approval/rejection workflow

## 📁 Optional Enhancements

### [ ] 12. Security Hardening
- [ ] Create `appsettings.local.json` for local overrides (already in .gitignore)
- [ ] Consider using environment variables for sensitive data:
  ```json
  "ApiToken": "${JIRA_API_TOKEN}"
  ```
- [ ] Set up token rotation schedule (recommended: every 90 days)

### [ ] 13. Additional Repository Providers
- [ ] Plan GitLab integration (if needed)
- [ ] Plan GitHub integration (if needed)
- [ ] Configure multiple repository providers

## 🚨 Security Reminders

- [ ] **NEVER** commit real credentials to version control
- [ ] Use app passwords/tokens, not your main account passwords
- [ ] Regularly rotate API tokens and app passwords
- [ ] Test with a non-production Jira project first
- [ ] Backup your configuration before making changes

## 📝 Documentation

### [ ] 14. Create Your Personal Config Notes
- [ ] Document your Jira project structure
- [ ] Note your repository naming conventions
- [ ] Record any custom workflow requirements
- [ ] Document team-specific status mappings

## ✅ Ready to Go!

When all items above are complete:
- [ ] Application builds and runs without errors
- [ ] Jira sync works and fetches relevant tasks
- [ ] Repository assignment and branch creation work
- [ ] Manual approval workflow is functional
- [ ] Jira updates are being posted

---

## 🆘 Need Help?

If you encounter issues:
1. Check the console output for specific error messages
2. Verify network connectivity to Jira and BitBucket
3. Test API credentials using their web interfaces first
4. Review the `CONFIGURATION.md` file for detailed setup instructions
5. Check the application logs for detailed error information

**Estimated Setup Time**: 30-45 minutes (excluding .NET SDK troubleshooting)
