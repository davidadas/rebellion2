#!/usr/bin/env python3

import math
import re
import shutil
import struct
import xml.etree.ElementTree as ElementTree
import zlib
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]
RESOURCE_ROOT = PROJECT_ROOT / "Assets" / "Resources"
ART_ROOT = RESOURCE_ROOT / "Art"
VIDEO_ROOT = RESOURCE_ROOT / "Videos"
JPEG_FIXTURE = PROJECT_ROOT / ".github" / "fixtures" / "blank-3840x2160.jpg"
VIDEO_FIXTURE = PROJECT_ROOT / ".github" / "fixtures" / "blank-video.mp4"


def get_metadata(root: Path) -> list[Path]:
    return sorted(root.rglob("*.meta")) if root.exists() else []


def get_sprite_dimensions(metadata_path: Path) -> tuple[int, int]:
    lines = metadata_path.read_text(encoding="utf-8").splitlines()
    width = 1
    height = 1

    for index, line in enumerate(lines):
        if line.strip() != "rect:":
            continue

        indent = len(line) - len(line.lstrip())
        values: dict[str, float] = {}
        for candidate in lines[index + 1 :]:
            if not candidate.strip():
                continue

            candidate_indent = len(candidate) - len(candidate.lstrip())
            if candidate_indent <= indent:
                break

            match = re.match(r"\s*(x|y|width|height):\s*(-?[\d.]+)\s*$", candidate)
            if match:
                values[match.group(1)] = float(match.group(2))

        if "width" not in values or "height" not in values:
            continue

        width = max(width, math.ceil(values.get("x", 0) + values["width"]))
        height = max(height, math.ceil(values.get("y", 0) + values["height"]))

    return width, height


def create_png(width: int, height: int) -> bytes:
    def create_chunk(chunk_type: bytes, data: bytes) -> bytes:
        checksum = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", checksum)

    compressor = zlib.compressobj(level=9)
    scanline = b"\0" + (b"\0\0\0\0" * width)
    compressed = bytearray()
    for _ in range(height):
        compressed.extend(compressor.compress(scanline))
    compressed.extend(compressor.flush())

    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + create_chunk(b"IHDR", header)
        + create_chunk(b"IDAT", bytes(compressed))
        + create_chunk(b"IEND", b"")
    )


def create_serialized_reference_images() -> int:
    png_cache: dict[tuple[int, int], bytes] = {}
    created = 0

    for metadata_path in get_metadata(ART_ROOT):
        asset_path = metadata_path.with_suffix("")
        if asset_path.exists():
            continue

        asset_path.parent.mkdir(parents=True, exist_ok=True)
        extension = asset_path.suffix.lower()
        if extension == ".png":
            dimensions = get_sprite_dimensions(metadata_path)
            if dimensions not in png_cache:
                png_cache[dimensions] = create_png(*dimensions)
            asset_path.write_bytes(png_cache[dimensions])
        elif extension in {".jpg", ".jpeg"}:
            width, height = get_sprite_dimensions(metadata_path)
            if width > 3840 or height > 2160:
                raise RuntimeError(
                    f"CI JPEG fixture is too small for {asset_path}: {width}x{height}"
                )
            shutil.copyfile(JPEG_FIXTURE, asset_path)
        else:
            raise RuntimeError(f"Unsupported CI image fixture type: {asset_path}")

        created += 1

    return created


def get_configured_art_paths() -> set[str]:
    paths: set[str] = set()
    for source_root_name in ("Configs", "Data"):
        source_root = RESOURCE_ROOT / source_root_name
        for xml_path in source_root.rglob("*.xml"):
            root = ElementTree.parse(xml_path).getroot()
            for element in root.iter():
                value = element.text.strip() if element.text else ""
                if value.startswith("Art/") and not element.tag.endswith("Root"):
                    paths.add(value)
    return paths


def resource_image_exists(path: Path) -> bool:
    return any(path.with_suffix(extension).exists() for extension in (".png", ".jpg", ".jpeg"))


def create_configured_resource_images() -> int:
    transparent_pixel = create_png(1, 1)
    created = 0

    for resource_path in sorted(get_configured_art_paths()):
        asset_path = RESOURCE_ROOT / resource_path
        if RESOURCE_ROOT not in asset_path.resolve().parents:
            raise RuntimeError(f"Configured art path escapes Resources: {resource_path}")
        if resource_image_exists(asset_path):
            continue

        placeholder_path = asset_path.with_suffix(".png")
        placeholder_path.parent.mkdir(parents=True, exist_ok=True)
        placeholder_path.write_bytes(transparent_pixel)
        created += 1

    return created


def create_video_assets() -> int:
    created = 0
    for metadata_path in get_metadata(VIDEO_ROOT):
        asset_path = metadata_path.with_suffix("")
        if asset_path.exists() or asset_path.suffix.lower() != ".mp4":
            continue

        asset_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(VIDEO_FIXTURE, asset_path)
        created += 1

    return created


def main() -> None:
    serialized_images = create_serialized_reference_images()
    configured_images = create_configured_resource_images()
    videos = create_video_assets()
    print(
        "Prepared CI asset stand-ins: "
        f"{serialized_images} serialized images, "
        f"{configured_images} configured images, {videos} videos."
    )


if __name__ == "__main__":
    main()
