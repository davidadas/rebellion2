"""Verify and package both desktop players without installed or user-authored content."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import tarfile
import tempfile
import zipfile

PLATFORMS = {
    "StandaloneWindows64": (".exe", "nfd.dll"),
    "StandaloneLinux64": (".x86_64", "libnfd.so"),
    "StandaloneOSX": (".app", "libnfd.dylib"),
}
EDITOR_ASSEMBLY = "ContentPackEditorAssembly.dll"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def file_hash(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def player_path(root, name, platform, game_name="rebellion2"):
    if platform == "StandaloneOSX" and name == "rebellion2-editor":
        return root / (game_name + ".app") / "Contents" / "Helpers" / (name + ".app")
    return root / (name + PLATFORMS[platform][0])


def player_data(root, name, platform, game_name="rebellion2"):
    if platform == "StandaloneOSX":
        return (
            player_path(root, name, platform, game_name)
            / "Contents"
            / "Resources"
            / "Data"
        )
    return root / (name + "_Data")


def archive_permissions(info):
    """Retain Unix permissions when available and mark known players executable on cross-builds."""
    name = Path(info.name)
    if (
        name.suffix == ".x86_64"
        or name.parent.name == "MacOS"
        or name.name == "UnityCrashHandler64"
    ):
        info.mode |= 0o755
    return info


def verify_native(path, platform):
    binary = path.read_bytes()
    if platform == "StandaloneWindows64":
        require(
            binary[:2] == b"MZ" and len(binary) >= 64, "Invalid Windows dialog library"
        )
        offset = int.from_bytes(binary[60:64], "little")
        require(
            binary[offset : offset + 6] == b"PE\0\0\x64\x86",
            "Dialog library must target Windows x64",
        )
    elif platform == "StandaloneLinux64":
        require(
            binary[:6] == b"\x7fELF\x02\x01" and binary[18:20] == b"\x3e\0",
            "Dialog library must target Linux x64",
        )
    else:
        require(
            binary[:4] == b"\xca\xfe\xba\xbe",
            "Dialog library must be a universal macOS binary",
        )
        count = int.from_bytes(binary[4:8], "big")
        require(
            len(binary) >= 8 + count * 20, "Invalid universal dialog library header"
        )
        cpus = {
            int.from_bytes(binary[8 + index * 20 : 12 + index * 20], "big")
            for index in range(count)
        }
        require(
            {0x01000007, 0x0100000C}.issubset(cpus),
            "Dialog library must include Intel and Apple Silicon",
        )


def collect_files(root, platform, game_name="rebellion2"):
    """Allow only Unity runtime artifacts and verify the companion's isolation and dependencies."""
    require(platform in PLATFORMS, f"Unsupported platform: {platform}")
    require(
        Path(game_name).name == game_name
        and game_name not in {".", "..", "rebellion2-editor"},
        "Use the game executable's base name without a directory",
    )
    require(root.is_dir(), f"Build folder not found: {root}")
    suffix, native_name = PLATFORMS[platform]
    allowed = {game_name + suffix}
    if platform != "StandaloneOSX":
        allowed.add("rebellion2-editor" + suffix)
        allowed.update({game_name + "_Data", "rebellion2-editor_Data"})
        if platform == "StandaloneWindows64":
            allowed.update(
                {
                    "UnityPlayer.dll",
                    "UnityCrashHandler64.exe",
                    "GameAssembly.dll",
                    "D3D12",
                    "dstorage.dll",
                    "dstoragecore.dll",
                    "MonoBleedingEdge",
                }
            )
            require((root / "UnityPlayer.dll").is_file(), "Missing UnityPlayer.dll")
        else:
            allowed.update({"UnityPlayer.so", "UnityCrashHandler64", "GameAssembly.so"})
            require((root / "UnityPlayer.so").is_file(), "Missing UnityPlayer.so")
    editor_path = player_path(root, "rebellion2-editor", platform, game_name)
    for name in (game_name, "rebellion2-editor"):
        player = player_path(root, name, platform, game_name)
        require(
            player.is_dir() if suffix == ".app" else player.is_file(),
            f"Missing player: {player}",
        )
        data = player_data(root, name, platform, game_name)
        require(data.is_dir(), f"Missing player data: {data}")
        # Both players share UnityPlayer; Linux keeps a Mono runtime in each player's data.
        # IL2CPP companions cannot share a single native GameAssembly library.
        require(
            (data / "Managed" / "GameAssembly.dll").is_file(),
            "Shared desktop bundles require Mono builds",
        )
        if platform == "StandaloneLinux64":
            require(
                any((data / "MonoBleedingEdge" / "x86_64").glob("*mono*.so")),
                f"Missing Mono runtime for: {name}",
            )
        require(
            (data / "Managed" / EDITOR_ASSEMBLY).is_file()
            == (name == "rebellion2-editor"),
            f"Incorrect editor assembly inclusion: {name}",
        )
        libraries = [
            path
            for path in (player if suffix == ".app" else data).rglob(native_name)
            if name == "rebellion2-editor" or not path.is_relative_to(editor_path)
        ]
        require(
            len(libraries) == (1 if name == "rebellion2-editor" else 0),
            f"Incorrect native dialog library inclusion: {name}",
        )
        if libraries:
            verify_native(libraries[0], platform)
        if name == "rebellion2-editor":
            notice_root = data.parent if suffix == ".app" else data
            notice = notice_root / "ThirdPartyNotices" / "NativeFileDialog.txt"
            require(
                notice.is_file() and notice.stat().st_size > 0,
                "Missing native dialog license notice",
            )

    files = []
    for child in sorted(root.iterdir()):
        if child.name not in allowed:
            continue
        require(
            not child.is_symlink(),
            f"Runtime root cannot be a symbolic link: {child.name}",
        )
        candidates = [child] if child.is_file() else sorted(child.rglob("*"))
        for path in candidates:
            relative = path.relative_to(root)
            require(
                not any(
                    part.lower() in {"content", "mods", "assets", "library", ".git"}
                    for part in relative.parts
                ),
                f"Development or user content in player data: {relative}",
            )
            if path.is_symlink():
                require(
                    platform == "StandaloneOSX", f"Unexpected symbolic link: {relative}"
                )
                require(
                    not os.path.isabs(os.readlink(path))
                    and path.resolve().is_relative_to(child.resolve()),
                    f"Symbolic link escapes app bundle: {relative}",
                )
                require(path.exists(), f"Broken symbolic link: {relative}")
                files.append(path)
                continue
            if not path.is_file():
                continue
            if path.suffix.lower() in {".pdb", ".mdb", ".meta"} or any(
                "DoNotShip" in part for part in relative.parts
            ):
                continue
            files.append(path)
    if platform == "StandaloneWindows64":
        require(
            any((root / "MonoBleedingEdge" / "EmbedRuntime").glob("*mono*")),
            "Missing shared Mono runtime",
        )
    require(files, "No runtime files found")
    return files


def stage(root, platform, destination, game_name="rebellion2"):
    """Stage only verified runtime files into a new installer payload directory."""
    root, destination = Path(root).resolve(), Path(destination).resolve()
    require(
        not destination.is_relative_to(root),
        "Staging output must be outside the build folder",
    )
    require(not destination.exists(), "Staging output already exists")
    files = collect_files(root, platform, game_name)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix=".rebellion2-stage-", dir=destination.parent
    ) as temporary:
        payload = Path(temporary) / "payload"
        payload.mkdir()
        for source in files:
            target = payload / source.relative_to(root)
            target.parent.mkdir(parents=True, exist_ok=True)
            if source.is_symlink():
                target.symlink_to(
                    os.readlink(source), target_is_directory=source.is_dir()
                )
            else:
                shutil.copy2(source, target)
        payload.rename(destination)
    return destination


def package(root, platform, output, game_name="rebellion2"):
    """Produce a verified runtime archive and its checksum, preserving the previous output on failure."""
    root, output = Path(root).resolve(), Path(output).resolve()
    require(
        not output.is_relative_to(root),
        "Package output must be outside the build folder",
    )
    files = collect_files(root, platform, game_name)
    manifest = {
        "platform": platform,
        "externalContent": "Content/catalog.xml",
        "files": [
            (
                {
                    "path": path.relative_to(root).as_posix(),
                    "symlink": os.readlink(path),
                }
                if path.is_symlink()
                else {
                    "path": path.relative_to(root).as_posix(),
                    "size": path.stat().st_size,
                    "sha256": file_hash(path),
                }
            )
            for path in files
        ],
    }
    manifest_bytes = (json.dumps(manifest, indent=2) + "\n").encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(
        prefix=".rebellion2-package-", dir=output.parent
    )
    os.close(descriptor)
    try:
        if platform == "StandaloneWindows64":
            with zipfile.ZipFile(
                temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=1
            ) as archive:
                for path in files:
                    archive.write(path, path.relative_to(root).as_posix())
                archive.writestr("bundle.json", manifest_bytes)
        else:
            import io

            with tarfile.open(temporary, "w:gz", compresslevel=1) as archive:
                for path in files:
                    archive.add(
                        path,
                        arcname=path.relative_to(root).as_posix(),
                        recursive=False,
                        filter=archive_permissions,
                    )
                info = tarfile.TarInfo("bundle.json")
                info.size, info.mode = (
                    len(manifest_bytes),
                    stat.S_IRUSR | stat.S_IRGRP | stat.S_IROTH,
                )
                archive.addfile(info, io.BytesIO(manifest_bytes))
        os.replace(temporary, output)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
    digest = file_hash(output)
    output.with_name(output.name + ".sha256").write_text(
        f"{digest}  {output.name}\n", encoding="utf-8"
    )
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=PLATFORMS, required=True)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument(
        "--game-name",
        default="rebellion2",
        help="Game executable base name without its platform extension",
    )
    outputs = parser.add_mutually_exclusive_group()
    outputs.add_argument("--output", type=Path)
    outputs.add_argument(
        "--stage-dir",
        type=Path,
        help="Create a clean runtime payload for installer packaging",
    )
    args = parser.parse_args()
    if args.output:
        print(package(args.build_dir, args.platform, args.output, args.game_name))
    elif args.stage_dir:
        print(stage(args.build_dir, args.platform, args.stage_dir, args.game_name))
    else:
        print(
            f"Verified {len(collect_files(args.build_dir.resolve(), args.platform, args.game_name))} runtime files"
        )


if __name__ == "__main__":
    main()
