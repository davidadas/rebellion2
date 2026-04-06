#!/bin/bash
set -e

PROJECT_PATH="${PROJECT_PATH:-.}"
TEST_RESULTS="${TEST_RESULTS:-TestResults.xml}"

# Roslynator needs DOTNET_ROOT to find the SDK
if [ -z "$DOTNET_ROOT" ]; then
    sdk_base="$(dotnet --info 2>/dev/null | sed -n 's/.*Base Path:[[:space:]]*//p' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
    if [ -n "$sdk_base" ]; then
        export DOTNET_ROOT="$(dirname "$(dirname "$sdk_base")")"
    fi
fi

# Detect platform and set defaults
case "$(uname -s)" in
    Darwin)
        UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity}"
        FRAMEWORK_PATH="${FRAMEWORK_PATH:-/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge/lib/mono/4.7.1-api}"
        ;;
    Linux)
        UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.4.0f1/Editor/Unity}"
        FRAMEWORK_PATH="${FRAMEWORK_PATH:-}"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        UNITY="${UNITY:-C:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Unity.exe}"
        FRAMEWORK_PATH="${FRAMEWORK_PATH:-}"
        ;;
    *)
        echo "Unknown platform: $(uname -s). Set UNITY env var manually."
        exit 1
        ;;
esac

usage() {
    echo "Usage: ./build.sh [command]"
    echo ""
    echo "Commands:"
    echo "  lint    Run Roslynator static analysis"
    echo "  test    Run EditMode tests via Unity"
    echo "  build   Build standalone player"
    echo "  clean   Remove build artifacts"
    echo "  all     Run lint + test"
    exit 1
}

ROSLYNATOR_ANALYZERS="${ROSLYNATOR_ANALYZERS:-$HOME/.nuget/packages/roslynator.analyzers/4.12.9/analyzers/dotnet/roslyn4.7/cs}"

do_lint() {
    local extra_args=()
    if [ -n "$FRAMEWORK_PATH" ]; then
        extra_args+=("-p:FrameworkPathOverride=$FRAMEWORK_PATH")
    fi

    # Full compilation check — only runs locally where Unity has generated the project files.
    # In CI the Unity test runner already proves compilation, so this is skipped.
    if [ -f GameAssembly.csproj ]; then
        echo "=== GameAssembly ==="
        dotnet build GameAssembly.csproj -verbosity:normal "${extra_args[@]}"
        echo ""
        echo "=== EditMode ==="
        dotnet build EditMode.csproj -verbosity:normal "${extra_args[@]}"
        echo ""
    fi

    # Roslynator uses a committed portable project file so it works in CI without Unity.
    echo "=== Roslynator ==="
    roslynator analyze GameAssembly.Lint.csproj \
        --analyzer-assemblies "$ROSLYNATOR_ANALYZERS" \
        --supported-diagnostics RCS1213 \
        --severity-level warning
    echo ""
    echo "Lint complete."
}

do_test() {
    "$UNITY" \
        -runTests \
        -testPlatform EditMode \
        -projectPath "$PROJECT_PATH" \
        -testResults "$TEST_RESULTS" \
        -batchmode \
        -nographics
    echo "Test results written to $TEST_RESULTS"
}

do_build() {
    "$UNITY" \
        -batchmode \
        -nographics \
        -projectPath "$PROJECT_PATH" \
        -buildTarget Win64 \
        -buildPlayerPath build/rebellion2.exe \
        -quit
    echo "Build complete."
}

do_clean() {
    rm -rf build/
    rm -f "$TEST_RESULTS"
    echo "Clean complete."
}

case "${1:-}" in
    lint)  do_lint ;;
    test)  do_test ;;
    build) do_build ;;
    clean) do_clean ;;
    all)   do_lint; do_test ;;
    "")    do_lint; do_test ;;
    *)     usage ;;
esac
