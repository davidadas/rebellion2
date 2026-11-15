"""Stage CI media on Windows and Unix hosts, preserving Unity metadata."""

import argparse
from pathlib import Path
import shutil


def excluded(path, exclude_metadata):
    return path.name == ".DS_Store" or (
        exclude_metadata and path.name.endswith(".meta")
    )


def remove_stale(path, exclude_metadata):
    if excluded(path, exclude_metadata):
        return
    if path.is_dir():
        for child in path.iterdir():
            remove_stale(child, exclude_metadata)
        if not any(path.iterdir()):
            path.rmdir()
    else:
        path.unlink()


def copy_tree(source, destination, *, mirror, exclude_metadata):
    destination.mkdir(parents=True, exist_ok=True)
    if mirror:
        for child in destination.iterdir():
            if not (source / child.name).exists():
                remove_stale(child, exclude_metadata)
    for child in source.iterdir():
        if excluded(child, exclude_metadata):
            continue
        target = destination / child.name
        if child.is_dir():
            copy_tree(
                child, target, mirror=mirror, exclude_metadata=exclude_metadata
            )
        else:
            shutil.copy2(child, target)


def install_build_media(media, assets):
    media, assets = Path(media), Path(assets)
    schema = media / "Content/Application/Schemas/game-config.xsd"
    if not schema.is_file():
        raise FileNotFoundError(f"Required build media is missing: {schema}")
    for source, destination, mirror, exclude_metadata in (
        (media / "Content", assets / "Content", True, True),
        (media / "Models/MainMenu", assets / "Art/Models/MainMenu", True, True),
        (media / "UnityAssets", assets, False, False),
    ):
        copy_tree(
            source, destination, mirror=mirror, exclude_metadata=exclude_metadata
        )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("media", type=Path)
    parser.add_argument("assets", type=Path)
    args = parser.parse_args()
    install_build_media(args.media, args.assets)


if __name__ == "__main__":
    main()
