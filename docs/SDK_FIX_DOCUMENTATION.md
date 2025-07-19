# .NET SDK MSB4236 Fix Documentation

## Problem Description
The error `MSB4236: The SDK 'Microsoft.NET.Sdk' specified could not be found` occurs on macOS when MSBuild cannot resolve the .NET SDK location. This typically happens due to:

- SDK resolution failures in `Microsoft.DotNet.MSBuildWorkloadSdkResolver`
- Corrupted workload manifests
- Version mismatches between `global.json` and installed SDKs
- Missing or corrupted SDK manifest files

## Root Cause Analysis
In our case, the issue was caused by:
1. **Version Mismatch**: `global.json` specified SDK version `8.0.404` but only .NET 9.0.301 was installed
2. **Missing Manifests**: The `sdk-manifests` directory was missing, causing workload resolver to return null
3. **SDK Probing Failure**: MSBuild couldn't locate the SDK through normal probing mechanisms

## Solution Applied

### 1. Fixed global.json Version Mismatch
```json
{
  "sdk": {
    "version": "9.0.301",  // Updated from 8.0.404
    "rollForward": "latestFeature"
  }
}
```

### 2. Set MSBuildSDKsPath Environment Variable
Added to `~/.zshrc`:
```bash
export MSBuildSDKsPath="/usr/local/share/dotnet/sdk/$(dotnet --version)/Sdks"
```

This bypasses the SDK resolver entirely and points MSBuild directly to the SDK location.

### 3. Fixed Package Version Conflicts
Updated NuGet package references to consistent versions:
- Microsoft.Extensions.Logging: 8.0.0 → 8.0.1
- Microsoft.Extensions.Configuration.Binder: 8.0.0 → 8.0.2
- Added Microsoft.Extensions.Http: 8.0.1

### 4. Resolved Namespace Conflicts
Added using aliases to prevent conflicts between `Tasked.Models.TaskStatus` and `System.Threading.Tasks.TaskStatus`:
```csharp
using TaskStatus = Tasked.Models.TaskStatus;
```

## Verification
- ✅ Build succeeds: `dotnet build` completes successfully
- ✅ Permanent fix: Environment variable persists across shell sessions
- ✅ Test script: `fix-dotnet-sdk.sh` validates the fix works

## Troubleshooting Script
Run `./fix-dotnet-sdk.sh` to:
- Verify .NET installation
- Apply the MSBuildSDKsPath fix
- Test with a sample project
- Add permanent configuration

## Alternative Solutions (if needed)
1. **Clear corrupted manifests**: `sudo rm -rf /usr/local/share/dotnet/sdk-manifests/`
2. **Disable workload resolver**: `export MSBuildEnableWorkloadResolver=false`
3. **Full clean reinstall**: Use official uninstall tool then reinstall SDK

## Prevention
- Keep `global.json` in sync with installed SDK versions
- Use `dotnet --list-sdks` to verify available versions
- Set `MSBuildSDKsPath` as a safety net for future SDK updates

## References
- [.NET SDK Resolution Issues](https://docs.microsoft.com/en-us/dotnet/core/tools/global-json)
- [MSBuild SDK Resolution](https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk)
- [.NET Uninstall Tool](https://docs.microsoft.com/en-us/dotnet/core/additional-tools/uninstall-tool)
