# Tasked Configuration Guide

This file explains how to configure `appsettings.json` for the Tasked application.

## 🔧 Jira Configuration

1. **Get your Jira details:**
   - BaseUrl: Your Atlassian domain (e.g., https://mycompany.atlassian.net)
   - Username: Your Atlassian email address
   - Project: Your Jira project key (found in project settings)

2. **Create a Jira API Token:**
   - Go to: https://id.atlassian.com/manage-profile/security/api-tokens
   - Click "Create API token"
   - Give it a label like "Tasked App"
   - Copy the token immediately (you can't see it again)

3. **Find your Project Key:**
   - In Jira, go to your project
   - Look at the URL or project settings
   - Usually 2-5 uppercase letters (e.g., PROJ, DEV, TASK)

## 🗂️ BitBucket Configuration  

1. **Create App Password:**
   - Go to: https://bitbucket.org/account/settings/app-passwords/
   - Click "Create app password"
   - Name: "Tasked Application"
   - Permissions needed:
     - Repositories: Read, Write
     - Pull requests: Read, Write
     - Issues: Read, Write (optional)

2. **Get Workspace/Organization:**
   - Your BitBucket workspace name (visible in URLs)
   - Usually your username or company name

3. **Project Key (if using BitBucket Projects):**
   - Found in your BitBucket project settings
   - Optional - leave empty if not using projects

## 🔄 Workflow Settings

- **RequireHumanApproval**: Set to `false` for full automation, `true` for manual review
- **AutoCreateRepositories**: Set to `true` only if you want the app to create new repos
- **AutoCreateBranches**: Recommended to keep `true` for branch creation
- **AutoUpdateJira**: Recommended to keep `true` for progress updates
- **MaxConcurrentTasks**: Limit parallel task processing (3-5 recommended)

## 📊 Example Configuration

```json
{
  "Jira": {
    "BaseUrl": "https://mycompany.atlassian.net",
    "Username": "john.doe@mycompany.com",
    "ApiToken": "ATATT3xFfGF0T...", 
    "Project": "DEV",
    "SyncIntervalMinutes": 15,
    "StatusFilter": ["To Do", "In Progress", "Ready for Development"]
  },
  "RepositoryProviders": [
    {
      "Name": "BitBucket",
      "BaseUrl": "https://api.bitbucket.org",
      "AppPassword": "ATBBxxx...",
      "Username": "your-bitbucket-username",
      "WorkspaceOrOrganization": "mycompany",
      "IsDefault": true,
      "AdditionalSettings": {
        "ProjectKey": "DEV"
      }
    }
  ]
}
```

## 🔒 Security Notes

- Never commit real credentials to version control
- Consider using environment variables for sensitive data
- Use app passwords, not your main account password
- Regularly rotate API tokens and app passwords

## 🧪 Testing Your Configuration

Once configured, the application will:
1. Connect to Jira and fetch tasks matching your status filter
2. Analyze task content to suggest repositories
3. Create feature branches with naming: `feature/{jira-key}-{task-title}`
4. Update Jira with progress comments
5. Provide an interactive console interface for manual oversight

## 🚨 Troubleshooting

- **Invalid credentials**: Check API tokens and usernames
- **No tasks found**: Verify project key and status filters
- **Repository errors**: Check workspace name and permissions
- **Network issues**: Verify URLs and network connectivity
