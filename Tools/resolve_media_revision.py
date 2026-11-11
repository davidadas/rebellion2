"""Resolve the content revision used by every build and test job."""

import argparse
from pathlib import Path
import re
import subprocess


def resolve(source_root, media_checkout, branch=""):
    """Use a pinned commit or the first existing companion branch, then main."""
    pin_path = Path(source_root) / ".github" / "media-revision"
    if pin_path.exists():
        revision = pin_path.read_text(encoding="utf-8").strip()
        if not re.fullmatch(r"[0-9a-fA-F]{40}", revision):
            raise ValueError(f"Expected a complete Git commit in {pin_path}")
        return revision.lower(), revision.lower()

    candidates = [branch, branch + "-media"] if branch else []
    candidates.append("main")
    for revision in candidates:
        reference = "refs/heads/" + revision
        result = subprocess.run(
            [
                "git", "-C", str(media_checkout), "ls-remote", "--exit-code",
                "--heads", "origin", reference,
            ],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode == 2:
            continue
        if result.returncode != 0:
            raise RuntimeError(result.stderr.strip() or "Media revision lookup failed")
        records = [line.split() for line in result.stdout.splitlines() if line.strip()]
        if (
            len(records) != 1
            or len(records[0]) != 2
            or records[0][1] != reference
            or not re.fullmatch(r"[0-9a-fA-F]{40}", records[0][0])
        ):
            raise RuntimeError(f"Expected one complete media commit for {reference}")
        return revision, records[0][0].lower()
    raise RuntimeError("The media repository has no matching branch or main branch")


def main():
    """Write the resolved revision and commit as GitHub Actions outputs."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=".", type=Path)
    parser.add_argument("--media-checkout", required=True, type=Path)
    parser.add_argument("--branch", default="")
    arguments = parser.parse_args()
    revision, sha = resolve(
        arguments.source_root, arguments.media_checkout, arguments.branch
    )
    print(f"revision={revision}")
    print(f"sha={sha}")


if __name__ == "__main__":
    main()
