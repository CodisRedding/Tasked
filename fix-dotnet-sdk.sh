#!/bin/bash
# Fix for MSB4236 "The SDK 'Microsoft.NET.Sdk' specified could not be found" on macOS
# Based on the comprehensive solution provided

echo "🔧 .NET SDK Fix Script for macOS"
echo "================================"

# Check current .NET installation
echo "📊 Current .NET Installation Info:"
dotnet --info
echo ""

echo "📋 Installed SDKs:"
dotnet --list-sdks
echo ""

# Get current SDK version
CURRENT_VERSION=$(dotnet --version)
echo "🎯 Current SDK Version: $CURRENT_VERSION"
echo ""

# Set the MSBuildSDKsPath
DOTNET_ROOT=$(dotnet --info | grep "Base Path" | awk '{print $3}' | sed 's|/sdk.*||')
SDK_PATH="$DOTNET_ROOT/sdk/$CURRENT_VERSION/Sdks"

echo "🛠️  Setting MSBuildSDKsPath to: $SDK_PATH"

# Check if the SDK path exists
if [ -d "$SDK_PATH" ]; then
    echo "✅ SDK directory exists"
    
    # Add to shell profile if not already present
    PROFILE_FILE="$HOME/.zshrc"
    EXPORT_LINE='export MSBuildSDKsPath="/usr/local/share/dotnet/sdk/$(dotnet --version)/Sdks"'
    
    if ! grep -q "MSBuildSDKsPath" "$PROFILE_FILE"; then
        echo "📝 Adding MSBuildSDKsPath to $PROFILE_FILE"
        echo "$EXPORT_LINE" >> "$PROFILE_FILE"
        echo "✅ Added to shell profile"
    else
        echo "ℹ️  MSBuildSDKsPath already configured in shell profile"
    fi
    
    # Set for current session
    export MSBuildSDKsPath="$SDK_PATH"
    echo "✅ Set for current session"
    
    # Test the fix
    echo ""
    echo "🧪 Testing the fix..."
    cd /tmp
    rm -rf TestSDKFix
    
    if dotnet new console -n TestSDKFix >/dev/null 2>&1; then
        echo "✅ Test project created successfully"
        cd TestSDKFix
        if dotnet build >/dev/null 2>&1; then
            echo "✅ Test project builds successfully"
            echo "🎉 Fix applied successfully!"
        else
            echo "❌ Test project failed to build"
        fi
        cd ..
        rm -rf TestSDKFix
    else
        echo "❌ Failed to create test project"
    fi
    
else
    echo "❌ SDK directory not found: $SDK_PATH"
    echo "💡 You may need to reinstall the .NET SDK"
    echo "   Download from: https://dotnet.microsoft.com/en-us/download/dotnet"
fi

echo ""
echo "📚 For more troubleshooting options:"
echo "   1. Clear SDK manifests: sudo rm -rf /usr/local/share/dotnet/sdk-manifests/"
echo "   2. Disable workload resolver: export MSBuildEnableWorkloadResolver=false"
echo "   3. Full reinstall: Use dotnet-uninstall-tool.sh --all then reinstall"
echo ""
echo "🔄 Reload your shell with: source ~/.zshrc"
