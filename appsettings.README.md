# ⚠️ Configuration Files Guide

## 📁 File Purpose

- **`appsettings.json`** - Template with default values (DO NOT EDIT)
- **`appsettings.local.json`** - Your personal credentials (created by setup)

## 🚀 Setup Instructions

**Never edit `appsettings.json` directly!** Instead:

```bash
dotnet run --setup
```

This creates `appsettings.local.json` with your real credentials that override the template values.

## 🔒 Security Note

- `appsettings.json` is committed to git (safe, no real credentials)
- `appsettings.local.json` is in `.gitignore` (contains real API tokens)
