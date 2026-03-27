#!/bin/bash
set -e

echo "Running Roslynator analyzers..."
echo ""

echo "=== GameAssembly ==="
dotnet build GameAssembly.csproj -verbosity:normal

echo ""
echo "=== EditMode ==="
dotnet build EditMode.csproj -verbosity:normal

echo ""
echo "✓ Analysis complete"
