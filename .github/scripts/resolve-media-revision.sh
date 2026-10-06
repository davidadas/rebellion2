#!/usr/bin/env bash

set -euo pipefail

media_repository=".ci/rebellion2-media"
main_ref="refs/remotes/origin/main"
revision="main"
sha="$(git -C "$media_repository" rev-parse "${main_ref}^{commit}")"
main_tree="$(git -C "$media_repository" rev-parse "${main_ref}^{tree}")"

if [[ -n "${PULL_REQUEST_BRANCH:-}" ]]; then
  for candidate in "$PULL_REQUEST_BRANCH" "$PULL_REQUEST_BRANCH-media"; do
    if ! git -C "$media_repository" ls-remote --exit-code --heads origin \
      "refs/heads/$candidate" >/dev/null; then
      continue
    fi

    candidate_ref="refs/remotes/origin/$candidate"
    git -C "$media_repository" fetch --no-tags origin \
      "refs/heads/$candidate:$candidate_ref"
    candidate_sha="$(git -C "$media_repository" rev-parse "${candidate_ref}^{commit}")"
    candidate_tree="$(git -C "$media_repository" rev-parse "${candidate_ref}^{tree}")"

    if [[ "$candidate_tree" == "$main_tree" ]]; then
      echo "Ignoring media branch $candidate because it has no changes from media main."
      continue
    fi

    if git -C "$media_repository" merge-base --is-ancestor "$main_ref" "$candidate_ref"; then
      revision="$candidate"
      sha="$candidate_sha"
      break
    fi

    if ! merged_tree="$(git -C "$media_repository" merge-tree --write-tree \
      "$main_ref" "$candidate_ref")"; then
      echo "Media branch $candidate conflicts with media main. Update the branch before rerunning CI."
      exit 1
    fi

    if [[ "$merged_tree" == "$main_tree" ]]; then
      echo "Ignoring media branch $candidate because its changes are already in media main."
      continue
    fi

    echo "Media branch $candidate has unmerged changes but does not contain current media main."
    echo "Update the branch from media main before rerunning CI."
    exit 1
  done
fi

echo "Using rebellion2-media revision: $revision"
echo "Using rebellion2-media commit: $sha"
echo "revision=$revision" >> "$GITHUB_OUTPUT"
echo "sha=$sha" >> "$GITHUB_OUTPUT"
