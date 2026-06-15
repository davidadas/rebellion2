#!/bin/bash
set -eE

PROJECT_PATH="${PROJECT_PATH:-.}"
TEST_RESULTS="${TEST_RESULTS:-TestResults.xml}"
ROSLYNATOR_ANALYZERS="${ROSLYNATOR_ANALYZERS:-$HOME/.nuget/packages/roslynator.analyzers/4.12.9/analyzers/dotnet/roslyn4.7/cs}"
GAME_LINT_PROJECT="${GAME_LINT_PROJECT:-GameAssembly.Lint.csproj}"
EDITOR_LINT_PROJECT="${EDITOR_LINT_PROJECT:-EditorAssembly.Lint.csproj}"

set_dotnet_root() {
    if [ -n "$DOTNET_ROOT" ] || ! command -v dotnet >/dev/null 2>&1; then
        return
    fi

    sdk_base="$(dotnet --info 2>/dev/null | sed -n 's/.*Base Path:[[:space:]]*//p' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
    if [ -n "$sdk_base" ]; then
        export DOTNET_ROOT="$(dirname "$(dirname "$sdk_base")")"
    fi
}

set_platform_defaults() {
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
}

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "FAIL: '$1' is required but is not installed."
        exit 1
    fi
}

run_unity_editmode_tests() {
    "$UNITY" \
        -runTests \
        -testPlatform EditMode \
        -projectPath "$PROJECT_PATH" \
        -testResults "$TEST_RESULTS" \
        -batchmode \
        -nographics \
        "$@"
}

parse_coverage_percent() {
    local tag="$1"
    local summary_xml="$2"

    sed -n "s:.*<$tag>\\([0-9.]*\\)</$tag>.*:\\1:p" "$summary_xml" | head -n 1
}

assert_decimal() {
    local label="$1"
    local value="$2"
    local source_path="$3"

    if ! [[ "$value" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then
        echo "FAIL: could not parse $label from $source_path (got '$value')"
        exit 1
    fi
}

assert_coverage_threshold() {
    local label="$1"
    local actual="$2"
    local threshold="$3"

    if [ "$(echo "$actual < $threshold" | bc -l)" = "1" ]; then
        echo "FAIL: $label coverage ${actual}% is below threshold ${threshold}%"
        return 1
    fi

    return 0
}

run_roslynator() {
    dotnet tool run roslynator -- "$@"
}

set_dotnet_root
set_platform_defaults

usage() {
    echo "Usage: ./build.sh [command]"
    echo ""
    echo "Commands:"
    echo "  format    Check formatting with CSharpier"
    echo "  xmlformat Format XML data files in-place with xmllint"
    echo "  lint      Run Roslynator static analysis"
    echo "  test      Run EditMode tests via Unity"
    echo "  coverage  Run EditMode tests with code coverage report"
    echo "  build     Build standalone player"
    echo "  clean     Remove build artifacts"
    echo "  all       Run format + lint + test"
    exit 1
}

do_format() {
    echo "=== Format ==="
    dotnet tool restore
    dotnet csharpier check Assets/
    echo ""
    echo "Format check complete."
}

do_xmlformat() {
    echo "=== XML Format ==="
    find Assets/Resources -name "*.xml" | while read -r f; do
        xmllint --format "$f" --output "$f"
        echo "Formatted $f"
    done
    echo ""
    echo "XML format complete."
}

do_lint() {
    dotnet tool restore

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

    echo "=== Format Rules ==="
    dotnet restore "$EDITOR_LINT_PROJECT"
    dotnet format "$EDITOR_LINT_PROJECT" style --verify-no-changes --severity error --no-restore
    echo ""

    echo "=== Naming Rules ==="
    if rg -n -P "private\\s+const\\s+[^=;]+?\\s+(?!_)[A-Za-z][A-Za-z0-9_]*\\s*=" Assets/Scripts Assets/Tests -g "*.cs"; then
        echo "Private constants must use _camelCase names."
        exit 1
    fi
    echo ""

    # Roslynator uses a committed portable project file so it works in CI without Unity.
    # Two passes: warnings are displayed but don't fail; errors do fail.
    echo "=== Roslynator ==="
    run_roslynator analyze "$GAME_LINT_PROJECT" \
        --analyzer-assemblies "$ROSLYNATOR_ANALYZERS" \
        --ignored-diagnostics CS0103 CS0234 CS0246 \
        --severity-level warning || true
    run_roslynator analyze "$GAME_LINT_PROJECT" \
        --analyzer-assemblies "$ROSLYNATOR_ANALYZERS" \
        --ignored-diagnostics CS0103 CS0234 CS0246 \
        --severity-level error
    run_roslynator analyze "$EDITOR_LINT_PROJECT" \
        --analyzer-assemblies "$ROSLYNATOR_ANALYZERS" \
        --ignored-diagnostics CS0103 CS0234 CS0246 \
        --severity-level warning || true
    run_roslynator analyze "$EDITOR_LINT_PROJECT" \
        --analyzer-assemblies "$ROSLYNATOR_ANALYZERS" \
        --ignored-diagnostics CS0103 CS0234 CS0246 \
        --severity-level error
    echo ""
    echo "Lint complete."
}

do_test() {
    run_unity_editmode_tests
    echo "Test results written to $TEST_RESULTS"
}

do_coverage() {
    require_command bc

    local coverage_dir="${COVERAGE_DIR:-Coverage}"
    run_unity_editmode_tests \
        -enableCodeCoverage \
        -coverageResultsPath "$coverage_dir" \
        -coverageOptions "generateHtmlReport;assemblyFilters:+GameAssembly;pathFilters:-Assets/Scripts/Game/Results/GameResults.cs"
    echo "Coverage report written to $coverage_dir/Report/index.html"

    # Keep thresholds in sync with .github/workflows/unity-tests.yml
    local summary_xml="$coverage_dir/Report/Summary.xml"
    local line_threshold=62.8
    local method_threshold=67.4

    if [ ! -f "$summary_xml" ]; then
        echo "Coverage summary not found at $summary_xml"
        exit 1
    fi

    local line_pct method_pct
    line_pct=$(parse_coverage_percent Linecoverage "$summary_xml")
    method_pct=$(parse_coverage_percent Methodcoverage "$summary_xml")

    assert_decimal "line coverage" "$line_pct" "$summary_xml"
    assert_decimal "method coverage" "$method_pct" "$summary_xml"

    printf "Line coverage:   %s%% (threshold %s%%)\n" "$line_pct" "$line_threshold"
    printf "Method coverage: %s%% (threshold %s%%)\n" "$method_pct" "$method_threshold"

    local failed=0
    if ! assert_coverage_threshold line "$line_pct" "$line_threshold"; then
        failed=1
    fi
    if ! assert_coverage_threshold method "$method_pct" "$method_threshold"; then
        failed=1
    fi

    if [ "$failed" = "1" ]; then
        exit 1
    fi

    echo "OK: all coverage thresholds met"
}

default_build_target() {
    case "$(uname -s)" in
        Darwin)
            echo "StandaloneOSX"
            ;;
        Linux)
            echo "StandaloneLinux64"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            echo "StandaloneWindows64"
            ;;
        *)
            echo "Unknown platform: $(uname -s). Set BUILD_TARGET env var manually."
            exit 1
            ;;
    esac
}

default_build_player_path() {
    case "$(uname -s)" in
        Darwin)
            echo "build/rebellion2.app"
            ;;
        Linux)
            echo "build/rebellion2.x86_64"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            echo "build/rebellion2.exe"
            ;;
        *)
            echo "Unknown platform: $(uname -s). Set BUILD_PLAYER_PATH env var manually."
            exit 1
            ;;
    esac
}

do_build() {
    local build_target="${BUILD_TARGET:-$(default_build_target)}"
    local build_player_path="${BUILD_PLAYER_PATH:-$(default_build_player_path)}"
    local resolved_player_path

    case "$build_player_path" in
        /*|[A-Za-z]:/*)
            resolved_player_path="$build_player_path"
            ;;
        *)
            resolved_player_path="$PROJECT_PATH/$build_player_path"
            ;;
    esac

    "$UNITY" \
        -batchmode \
        -nographics \
        -projectPath "$PROJECT_PATH" \
        -buildTarget "$build_target" \
        -executeMethod StandalonePlayerBuild.Build \
        -buildPlayerPath "$build_player_path" \
        -quit

    if [ ! -e "$resolved_player_path" ]; then
        echo "FAIL: player build output not found at $resolved_player_path"
        exit 1
    fi

    echo "Build complete: $resolved_player_path"
}

do_clean() {
    rm -rf build/
    rm -f "$TEST_RESULTS"
    echo "Clean complete."
}

do_all() {
    trap 'echo ""; echo "Resist the dark side and fix those tests..."' ERR
    do_format
    do_lint
    do_coverage
    echo ""
    echo "Your tests pass. The force is strong with this one."
}

case "${1:-}" in
    format)    do_format ;;
    xmlformat) do_xmlformat ;;
    lint)      do_lint ;;
    test)     do_test ;;
    coverage) do_coverage ;;
    build)    do_build ;;
    clean)    do_clean ;;
    all)      do_all ;;
    "")       do_all ;;
    *)        usage ;;
esac
