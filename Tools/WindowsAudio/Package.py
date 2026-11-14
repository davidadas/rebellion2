"""Packages only the files needed to run the standalone Windows audio check."""

import argparse
from pathlib import Path
import zipfile


def package(source: Path, destination: Path) -> Path:
    """Creates a portable ZIP without including reports or other checkout files."""
    if destination.suffix.lower() != ".zip":
        raise ValueError("The output must be a .zip file.")
    names = ("Run.cmd", "Run.ps1", "WindowsAudioProbe.cs", "Read me.txt")
    payloads = {name: (source / name).read_bytes() for name in names}
    payloads["Run.cmd"] = (
        payloads["Run.cmd"].replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
    )
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, content in payloads.items():
            archive.writestr(name, content)
    return destination


def main() -> None:
    """Builds the shareable archive at an explicitly supplied destination."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="Destination for the portable .zip file")
    arguments = parser.parse_args()
    print(package(Path(__file__).resolve().parent, arguments.output.resolve()))


if __name__ == "__main__":
    main()
