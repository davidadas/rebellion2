#!/bin/bash
set -e

UNITY="${UNITY:-C:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Unity.exe}"
PROJECT_PATH="${PROJECT_PATH:-.}"
TEST_RESULTS="${TEST_RESULTS:-TestResults.xml}"

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

do_lint() {
    echo "=== GameAssembly ==="
    dotnet build GameAssembly.csproj -verbosity:normal
    echo ""
    echo "=== EditMode ==="
    dotnet build EditMode.csproj -verbosity:normal
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
    *)     usage ;;
esac
