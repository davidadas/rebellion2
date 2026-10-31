#!/usr/bin/env bash
set -euo pipefail

action="${1:?usage: private-unity-cache.sh <restore|save> <scope> <target> <base-revision> <current-revision> <media-revision>}"
scope="${2:?cache scope is required}"
target="${3:?cache target is required}"
base_revision="${4:?base revision is required}"
current_revision="${5:?current revision is required}"
media_revision="${6:?media revision is required}"

: "${UNITY_VERSION:?UNITY_VERSION is required}"
: "${R2_ACCESS_KEY_ID:?R2_ACCESS_KEY_ID is required}"
: "${R2_SECRET_ACCESS_KEY:?R2_SECRET_ACCESS_KEY is required}"
: "${R2_ENDPOINT:?R2_ENDPOINT is required}"
: "${R2_BUCKET:?R2_BUCKET is required}"

command -v aws >/dev/null 2>&1 || {
    echo "The AWS CLI is required for the private Unity cache."
    exit 1
}
command -v zstd >/dev/null 2>&1 || {
    echo "zstd is required for the private Unity cache."
    exit 1
}

export AWS_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID"
export AWS_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY"
export AWS_DEFAULT_REGION=auto
export AWS_EC2_METADATA_DISABLED=true

cache_schema="v1"
cache_root="ci/unity-library/$cache_schema/$UNITY_VERSION/$scope/$target/$media_revision"
workspace="${GITHUB_WORKSPACE:-$PWD}"
temporary_root="${RUNNER_TEMP:-/tmp}"
archive_path="$temporary_root/rebellion2-unity-library.tar.zst"
checksum_path="$archive_path.sha256"

configuration_hash="$({
    find "$workspace/Packages" "$workspace/ProjectSettings" -type f -print0
} | sort -z | xargs -0 sha256sum | sha256sum | awk '{print $1}')"

object_key() {
    local revision="$1"
    printf '%s/%s/%s/library.tar.zst' "$cache_root" "$configuration_hash" "$revision"
}

object_exists() {
    local key="$1"
    aws --endpoint-url "$R2_ENDPOINT" s3api head-object \
        --bucket "$R2_BUCKET" \
        --key "$key" >/dev/null 2>&1
}

restore_key() {
    local key="$1"
    echo "Restoring private Unity cache: $key"
    aws --endpoint-url "$R2_ENDPOINT" s3 cp \
        "s3://$R2_BUCKET/$key" "$archive_path" --no-progress
    aws --endpoint-url "$R2_ENDPOINT" s3 cp \
        "s3://$R2_BUCKET/$key.sha256" "$checksum_path" --no-progress

    local expected_checksum
    expected_checksum="$(tr -d '[:space:]' < "$checksum_path")"
    printf '%s  %s\n' "$expected_checksum" "$archive_path" | sha256sum --check --status

    rm -rf "$workspace/Library"
    tar --extract --use-compress-program=unzstd --file="$archive_path" --directory="$workspace"
    rm -f "$archive_path" "$checksum_path"
}

restore_cache() {
    local current_key base_key
    current_key="$(object_key "$current_revision")"
    base_key="$(object_key "$base_revision")"

    for key in "$current_key" "$base_key"; do
        if ! object_exists "$key"; then
            continue
        fi
        if restore_key "$key"; then
            echo "Private Unity cache restored."
            return 0
        fi
        echo "Private Unity cache validation failed; continuing with a clean Library."
        rm -rf "$workspace/Library"
        rm -f "$archive_path" "$checksum_path"
    done

    echo "No compatible private Unity cache was found; continuing with a clean Library."
}

save_cache() {
    local key
    key="$(object_key "$current_revision")"
    if object_exists "$key"; then
        echo "Private Unity cache already exists: $key"
        return 0
    fi
    if [ ! -d "$workspace/Library" ]; then
        echo "Unity did not create a Library directory."
        exit 1
    fi

    echo "Creating private Unity cache: $key"
    tar --create --use-compress-program='zstd -T0 -3' \
        --file="$archive_path" --directory="$workspace" Library
    sha256sum "$archive_path" | awk '{print $1}' > "$checksum_path"

    aws --endpoint-url "$R2_ENDPOINT" s3 cp \
        "$archive_path" "s3://$R2_BUCKET/$key" --no-progress
    aws --endpoint-url "$R2_ENDPOINT" s3 cp \
        "$checksum_path" "s3://$R2_BUCKET/$key.sha256" --no-progress
    rm -f "$archive_path" "$checksum_path"
    echo "Private Unity cache saved."
}

case "$action" in
    restore)
        restore_cache
        ;;
    save)
        save_cache
        ;;
    *)
        echo "Unknown private Unity cache action: $action"
        exit 1
        ;;
esac
