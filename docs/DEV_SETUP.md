# Developer Setup Guide

## Prerequisites
- .NET 9.0 SDK
- Git
- Access to Jira instance
- Access to BitBucket workspace (or other repository provider)

## First Time Setup

1. **Clone and Build**
   ```bash
   git clone <repository-url>
   cd Tasked
   dotnet restore
   dotnet build
   ```

2. **Configuration**
   ```bash
   dotnet run --setup
   ```
   Follow the interactive wizard to configure:
   - Jira credentials and JQL query
   - BitBucket workspace and app password
   - Workflow preferences

3. **Verify Setup**
   ```bash
   dotnet run --token-health
   ```

## Development Commands

- `dotnet run --setup` - Interactive configuration wizard
- `dotnet run --token-health` - Check API token status
- `dotnet run --update-token` - Replace expired/compromised tokens
- `dotnet run` - Run main application
- `dotnet build` - Build project
- `dotnet test` - Run tests (when available)

## Configuration Files

- **`appsettings.json`** - Template configuration (commit this)
- **`appsettings.local.json`** - Your real credentials (NEVER commit this)
- **`.gitignore`** - Ensures local config stays private

## Progressive Setup

The setup wizard supports interruption and resumption:
- Each section (Jira, BitBucket, Workflow) is saved immediately
- You can stop setup and resume later without losing progress
- Check `appsettings.local.json` to see current progress

## Debugging Setup Issues

1. **Check current configuration:**
   ```bash
   cat appsettings.local.json
   ```

2. **Test API connections:**
   ```bash
   dotnet run --token-health
   ```

3. **Reset configuration:**
   ```bash
   rm appsettings.local.json
   dotnet run --setup
   ```

## Architecture Notes

- **Two-file config strategy**: Template + local overrides
- **Progressive saving**: No lost work during setup
- **Smart detection**: Handles existing vs new configurations
- **JQL-based queries**: Flexible task filtering
- **Provider-specific UI**: Clear labeling for different services

## Contributing

1. Create feature branch
2. Make changes
3. Ensure `dotnet build` succeeds with no warnings
4. Test setup wizard with your changes
5. Update documentation if needed
6. Create pull request

## Troubleshooting

### Common Issues
- **Missing .NET SDK**: Install from https://dotnet.microsoft.com/download
- **Build warnings**: We maintain zero-warning policy
- **Configuration errors**: Use `--token-health` to diagnose
- **Setup interruption**: Progressive saving should preserve progress

### Getting Help
- Check `docs/DEVELOPMENT.md` for recent changes
- Review `docs/ADR.md` for architectural decisions
- Look at existing configuration in `appsettings.json` for structure
