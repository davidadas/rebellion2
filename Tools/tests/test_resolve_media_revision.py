import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location(
    "resolve_media_revision", Path(__file__).parents[1] / "resolve_media_revision.py"
)
resolver = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(resolver)


class ResolveMediaRevisionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        (self.source / ".github").mkdir(parents=True)
        self.media = self.root / "media"
        self.git("init", "--initial-branch=main", str(self.media))
        self.git("config", "user.name", "Build tests", repo=self.media)
        self.git("config", "user.email", "build-tests@example.invalid", repo=self.media)
        self.git("commit", "--allow-empty", "-m", "Creating test content", repo=self.media)
        self.sha = self.git("rev-parse", "HEAD", repo=self.media)
        self.git("remote", "add", "origin", str(self.media), repo=self.media)

    def test_resolve_pinned_commit_ignores_branch_names(self):
        (self.source / ".github" / "media-revision").write_text(
            self.sha.upper() + "\n", encoding="utf-8"
        )
        self.assertEqual(
            (self.sha, self.sha), resolver.resolve(self.source, self.media, "combat")
        )

    def test_resolve_invalid_pin_does_not_fall_back(self):
        for value in ("", "main", self.sha[:12], self.sha + "\nmain"):
            with self.subTest(value=value):
                (self.source / ".github" / "media-revision").write_text(
                    value, encoding="utf-8"
                )
                with self.assertRaises(ValueError):
                    resolver.resolve(self.source, self.media)

    def test_resolve_matching_branch_precedes_companion(self):
        self.git("branch", "combat", repo=self.media)
        self.git("branch", "combat-media", repo=self.media)
        self.assertEqual(
            ("combat", self.sha), resolver.resolve(self.source, self.media, "combat")
        )

    def test_resolve_companion_branch_precedes_main(self):
        self.git("branch", "combat-media", repo=self.media)
        self.assertEqual(
            ("combat-media", self.sha), resolver.resolve(self.source, self.media, "combat")
        )

    def test_resolve_no_matching_branch_uses_main(self):
        self.assertEqual(
            ("main", self.sha), resolver.resolve(self.source, self.media, "combat")
        )

    def test_resolve_no_pull_request_uses_main(self):
        self.assertEqual(("main", self.sha), resolver.resolve(self.source, self.media))

    def test_resolve_missing_main_reports_failure(self):
        self.git("branch", "-m", "content", repo=self.media)
        with self.assertRaises(RuntimeError):
            resolver.resolve(self.source, self.media)

    def test_resolve_remote_failure_does_not_fall_back(self):
        self.git("remote", "set-url", "origin", str(self.root / "missing"), repo=self.media)
        with self.assertRaises(RuntimeError):
            resolver.resolve(self.source, self.media, "combat")

    def git(self, *arguments, repo=None):
        command = ["git"]
        if repo is not None:
            command.extend(["-C", str(repo)])
        result = subprocess.run(
            [*command, *arguments], capture_output=True, text=True, check=True
        )
        return result.stdout.strip()
