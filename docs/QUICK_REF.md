# Tasked - Quick Reference

## 🚀 **Essential Commands**
```bash
dotnet run --setup          # Interactive configuration wizard
dotnet run --token-health   # Check API token status  
dotnet run --update-token   # Replace expired tokens
dotnet run                  # Run main application
```

## 📁 **Key Files**
- `appsettings.json` - Template config (commit this) 
- `appsettings.local.json` - Real credentials (NEVER commit)
- `docs/DEVELOPMENT.md` - Full development history
- `docs/ADR.md` - Architecture decisions
- `docs/DEV_SETUP.md` - Developer setup guide

## 🔧 **Recent Major Features**
- ✅ Progressive configuration saving
- ✅ Smart existing config detection  
- ✅ Token health monitoring system
- ✅ JQL-based task queries
- ✅ BitBucket workspace auto-discovery
- ✅ Provider-specific UI labels
- ✅ Zero compiler warnings

## 🛡️ **Security Notes**
- Real credentials ONLY in `appsettings.local.json`
- This file is gitignored - never commit it
- Use placeholder values in `appsettings.json` template

## 🏗️ **Architecture Highlights**
- Two-file configuration strategy
- Progressive saving with JSON merging
- File-based credential storage (not database)
- Async/await patterns throughout
- Spectre.Console for rich UI

## 🐛 **Troubleshooting**
1. Check `appsettings.local.json` for current config
2. Run `dotnet run --token-health` to test connections
3. Delete `appsettings.local.json` to reset setup
4. Ensure `dotnet build` shows zero warnings

## 📈 **Development Workflow**
1. Make changes
2. Run `dotnet build` (must be warning-free)
3. Test with `dotnet run --setup`
4. Verify with `dotnet run --token-health`
5. Document in `docs/DEVELOPMENT.md` if significant
