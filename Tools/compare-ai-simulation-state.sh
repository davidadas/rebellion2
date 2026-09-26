#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 4 ]]; then
    echo "Usage: $0 <baseline-json> <baseline-save> <candidate-json> <candidate-save>" >&2
    exit 2
fi

baseline_json=$1
baseline_save=$2
candidate_json=$3
candidate_save=$4

for required_file in "$baseline_json" "$baseline_save" "$candidate_json" "$candidate_save"; do
    if [[ ! -f "$required_file" ]]; then
        echo "Missing comparison input: $required_file" >&2
        exit 2
    fi
done

comparison_dir=$(mktemp -d "${TMPDIR:-/tmp}/rebellion2-ai-state.XXXXXX")
trap 'rm -rf "$comparison_dir"' EXIT

normalize_report() {
    local source_path=$1
    local destination_path=$2
    jq --sort-keys 'del(.OutputPath)' "$source_path" > "$destination_path"
}

normalize_save() {
    local source_path=$1
    local destination_path=$2
    sed -E \
        -e 's#<LastSavedUtc>[^<]*</LastSavedUtc>#<LastSavedUtc></LastSavedUtc>#' \
        -e 's#<SaveDisplayName>[^<]*</SaveDisplayName>#<SaveDisplayName></SaveDisplayName>#' \
        "$source_path" \
        | perl -pe '
            s{<MovementGroupID>([^<]+)</MovementGroupID>}
             {"<MovementGroupID>" . ($movement_groups{$1} //= sprintf("movement-group-%06d", ++$movement_group_count)) . "</MovementGroupID>"}ge
        ' > "$destination_path"
}

normalize_report "$baseline_json" "$comparison_dir/baseline.json"
normalize_report "$candidate_json" "$comparison_dir/candidate.json"
normalize_save "$baseline_save" "$comparison_dir/baseline.sav"
normalize_save "$candidate_save" "$comparison_dir/candidate.sav"

baseline_report_hash=$(shasum -a 256 "$comparison_dir/baseline.json" | awk '{print $1}')
candidate_report_hash=$(shasum -a 256 "$comparison_dir/candidate.json" | awk '{print $1}')
baseline_save_hash=$(shasum -a 256 "$comparison_dir/baseline.sav" | awk '{print $1}')
candidate_save_hash=$(shasum -a 256 "$comparison_dir/candidate.sav" | awk '{print $1}')

echo "Baseline report:  $baseline_report_hash"
echo "Candidate report: $candidate_report_hash"
echo "Baseline save:    $baseline_save_hash"
echo "Candidate save:   $candidate_save_hash"

comparison_failed=0
if ! cmp -s "$comparison_dir/baseline.json" "$comparison_dir/candidate.json"; then
    echo "Simulation reports differ." >&2
    diff -u "$comparison_dir/baseline.json" "$comparison_dir/candidate.json" | head -200 >&2 || true
    comparison_failed=1
fi

if ! cmp -s "$comparison_dir/baseline.sav" "$comparison_dir/candidate.sav"; then
    echo "Persisted game states differ." >&2
    comparison_failed=1
fi

if [[ $comparison_failed -ne 0 ]]; then
    exit 1
fi

random_index=$(sed -nE 's#.*<RandomIndex>([0-9]+)</RandomIndex>.*#\1#p' "$comparison_dir/candidate.sav")
echo "Exact simulation state match. Random index: ${random_index:-unknown}"
