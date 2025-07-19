# 🔧 Quick Reference: appsettings.json Values to Replace

Copy this as a reference while updating your `appsettings.json` file.

## 📝 Current Placeholders → What to Replace

### Jira Section
```json
"BaseUrl": "https://your-domain.atlassian.net"
```
**Replace with:** Your actual Jira URL (e.g., `https://mycompany.atlassian.net`)

```json
"Username": "your-email@domain.com"
```
**Replace with:** Your Atlassian account email address

```json
"ApiToken": "your-api-token"
```
**Replace with:** API token from https://id.atlassian.com/manage-profile/security/api-tokens

```json
"Project": "YOUR_PROJECT_KEY"
```
**Replace with:** Your Jira project key (2-5 letters, e.g., `DEV`, `PROJ`, `TASK`)

### BitBucket Section
```json
"AppPassword": "your-bitbucket-app-password",
"Username": "your-bitbucket-username"
```
**Replace with:** 
- **App Password** (until Sept 9, 2025): From https://bitbucket.org/account/settings/app-passwords/
- **API Token** (after Sept 9, 2025): From https://id.atlassian.com/manage-profile/security/api-tokens
- **Username**: Your BitBucket username

> **Transition Note**: BitBucket App Passwords will be deprecated on September 9, 2025. After this date, use `ApiToken` instead of `AppPassword` and authenticate with your Atlassian API token.

```json
"WorkspaceOrOrganization": "your-workspace"
```
**Replace with:** Your BitBucket workspace name (visible in BitBucket URLs)

```json
"ProjectKey": "YOUR_PROJECT"
```
**Replace with:** Your BitBucket project key (or remove if not using projects)

## 🎯 Example Completed Configuration

```json
{
  "Jira": {
    "BaseUrl": "https://acmetech.atlassian.net",
    "Username": "john.doe@acmetech.com",
    "ApiToken": "ATATT3xFfGF0T4567890abcdefghijklmnop",
    "Project": "DEV",
    "SyncIntervalMinutes": 15,
    "StatusFilter": ["To Do", "In Progress", "Ready for Development"]
  },
  "Database": {
    "ConnectionString": "Data Source=tasked.db"
  },
  "RepositoryProviders": [
    {
      "Name": "BitBucket",
      "BaseUrl": "https://api.bitbucket.org",
      "AppPassword": "ATBBxxxxxxxxxxxxxxxxxx",
      "Username": "john.doe",
      "WorkspaceOrOrganization": "acmetech",
      "IsDefault": true,
      "AdditionalSettings": {
        "ProjectKey": "DEV"
      }
    }
  ],
  "Workflow": {
    "RequireHumanApproval": true,
    "AutoCreateRepositories": false,
    "AutoCreateBranches": true,
    "AutoUpdateJira": true,
    "MaxConcurrentTasks": 3
  }
}
```

## 🔍 How to Find Each Value

| Value | Where to Find It |
|-------|------------------|
| **Jira BaseUrl** | Your Jira browser URL (everything before `/jira/` or `/secure/`) |
| **Jira Project Key** | Jira project settings → Details, or visible in issue keys (e.g., `DEV-123`) |
| **Jira API Token** | Atlassian Account → Security → API tokens → Create API token |
| **BitBucket Workspace** | BitBucket URL structure: `https://bitbucket.org/{workspace}/` |
| **BitBucket App Password** | BitBucket → Personal settings → App passwords → Create app password |
| **BitBucket Project Key** | BitBucket project → Settings (optional, can be left as "YOUR_PROJECT") |

## ⚠️ Security Notes

- API tokens are like passwords - never share them
- Copy tokens immediately when created (you can't see them again)
- Test with a development/staging environment first
- Keep a backup of your configuration (without the tokens)

## 🧪 Testing Your Configuration

After updating the values:
1. Save `appsettings.json`
2. Run `dotnet run` (once .NET SDK issue is resolved)
3. Try "🔄 Sync from Jira" in the console interface
4. Verify it connects and fetches your tasks

## 🆘 Common Issues

**"Unauthorized" errors:** Check your email/API token combination
**"Project not found":** Verify the project key spelling and case
**"Workspace not found":** Check BitBucket workspace name spelling  
**No tasks found:** Verify your Jira status filter matches your workflow
