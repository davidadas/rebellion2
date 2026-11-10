#!/usr/bin/env python3
"""Build and verify the native vector library for a desktop target."""

import argparse
import ctypes
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import uuid


TARGET_FILES = {
    "win-x64": "RebellionTacticalMath.dll",
    "linux-x64": "libRebellionTacticalMath.so",
    "osx": "libRebellionTacticalMath.dylib",
}

IMPORT_NAMESPACE = uuid.UUID("d7ccae5c-ad2b-466c-8605-9b46c09a261e")


def write_import_metadata(output, target):
    """Author deterministic runtime import settings beside each generated library."""
    def guid(name):
        return uuid.uuid5(IMPORT_NAMESPACE, name).hex

    for folder, identity in [(output, "TacticalMath"), (output / target, "TacticalMath/" + target)]:
        folder.with_name(folder.name + ".meta").write_text(
            f"fileFormatVersion: 2\nguid: {guid(identity)}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n"
        )
    editor_os, cpu, standalone = {
        "win-x64": ("Windows", "x86_64", "Win64"),
        "linux-x64": ("Linux", "x86_64", "Linux64"),
        "osx": ("OSX", "AnyCPU", "OSXUniversal"),
    }[target]
    library = output / target / TARGET_FILES[target]
    library.with_name(library.name + ".meta").write_text(
        f"fileFormatVersion: 2\nguid: {guid('TacticalMath/' + target + '/' + library.name)}\n"
        "PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
        "  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n"
        "  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n"
        "  validateReferences: 1\n  platformData:\n"
        "  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n"
        "  - first:\n      Editor: Editor\n    second:\n      enabled: 1\n      settings:\n"
        f"        CPU: {cpu}\n        OS: {editor_os}\n"
        f"  - first:\n      Standalone: {standalone}\n    second:\n      enabled: 1\n"
        f"      settings:\n        CPU: {cpu}\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    )


def run(*arguments):
    """Run a build or verification command and reject nonzero exit codes."""
    subprocess.run([str(value) for value in arguments], check=True)


def load_library(path):
    """Load the produced library and check its public scalar ABI."""
    library = ctypes.CDLL(str(path.resolve()))
    for name in ("abi", "significand_bits", "vector_size"):
        function = getattr(library, "tactical_math_" + name)
        function.argtypes = []
        function.restype = ctypes.c_uint32
    if library.tactical_math_abi() != 1 or library.tactical_math_vector_size() != 12:
        raise RuntimeError("The native vector ABI does not match the runtime adapter.")
    print("Native significand bits:", library.tactical_math_significand_bits())
    length = library.tactical_math_length
    length.argtypes = [ctypes.c_float] * 3 + [ctypes.POINTER(ctypes.c_float)]
    length.restype = ctypes.c_int
    result = ctypes.c_float()
    if length(3, 4, 0, ctypes.byref(result)) != 1 or result.value != 5:
        raise RuntimeError("The loaded native library failed its length contract.")


def verify_windows_dependencies(library, compiler):
    """Reject non-system runtime DLL imports that are not part of the generated package."""
    inspector = Path(compiler).with_name("objdump.exe")
    output = subprocess.check_output([str(inspector), "-p", str(library)], text=True)
    imports = re.findall(r"^\s*DLL Name:\s*(\S+)", output, re.MULTILINE)
    if not imports:
        raise RuntimeError("The Windows import table could not be verified.")
    for dependency in imports:
        name = dependency.lower()
        if name not in ("kernel32.dll", "ucrtbase.dll", "msvcrt.dll") and not name.startswith("api-ms-win-crt-"):
            raise RuntimeError(f"The native library requires an unstaged runtime: {dependency}")


def main():
    """Configure, build, test, and copy a target-native library into its import directory."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path("Native/TacticalMath"))
    parser.add_argument("--build-directory", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, default=Path("Assets/Plugins/TacticalMath"))
    parser.add_argument("--target", choices=TARGET_FILES, required=True)
    parser.add_argument("--compiler", default=None)
    args = parser.parse_args()
    hosts = {"win-x64": "Windows", "linux-x64": "Linux", "osx": "Darwin"}
    if platform.system() != hosts[args.target]:
        parser.error("Build each target on its native operating system.")
    machine = platform.machine().lower()
    if machine not in ("amd64", "x86_64", "arm64", "aarch64"):
        parser.error("The desktop build requires a 64-bit host.")
    if args.target != "osx" and machine not in ("amd64", "x86_64"):
        parser.error("This desktop target requires an x86-64 host.")
    compiler = args.compiler or ("clang++" if args.target == "osx" else "g++")
    compiler_path = shutil.which(compiler)
    if compiler_path is None:
        parser.error(f"Required C++ compiler is unavailable: {compiler}")
    build = args.build_directory.resolve()
    configure = [
        "cmake", "-S", args.source.resolve(), "-B", build,
        "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=ON",
        f"-DCMAKE_CXX_COMPILER={compiler_path}",
    ]
    if args.target == "win-x64":
        make = Path(compiler_path).with_name("mingw32-make.exe")
        if not make.is_file():
            parser.error("The GCC toolchain must include mingw32-make.exe.")
        configure.extend(["-G", "MinGW Makefiles", f"-DCMAKE_MAKE_PROGRAM={make}"])
    if args.target == "osx":
        configure.extend(["-DCMAKE_OSX_ARCHITECTURES=x86_64;arm64", "-DCMAKE_OSX_DEPLOYMENT_TARGET=11.0"])
    run(*configure)
    run("cmake", "--build", build, "--config", "Release", "--parallel", os.cpu_count() or 2)
    run("ctest", "--test-dir", build, "-C", "Release", "--output-on-failure")
    output = args.output_directory.resolve() / args.target
    output.mkdir(parents=True, exist_ok=True)
    library = build / TARGET_FILES[args.target]
    if not library.is_file():
        raise RuntimeError(f"The build did not produce {library.name}.")
    if args.target == "osx":
        run("lipo", library, "-verify_arch", "x86_64", "arm64")
    if args.target == "win-x64":
        verify_windows_dependencies(library, compiler_path)
    load_library(library)
    shutil.copy2(library, output / library.name)
    write_import_metadata(args.output_directory.resolve(), args.target)
    print("Installed", output / library.name)


if __name__ == "__main__":
    main()
