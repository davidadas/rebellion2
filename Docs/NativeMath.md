# Native vector math

Combat uses a required desktop native library for complete retained vector expressions. Its
`long double` arithmetic follows each target compiler's native type. Explicit 53-bit movement
calculations remain managed and separate. The runtime does not substitute an approximate backend
when the library is missing.

Install CMake 3.20 or newer, Python 3, and a GCC toolchain on Windows or Linux, or the Xcode command
line tools on macOS. The Windows GCC toolchain must include `mingw32-make`. Build the host library
before opening a clean checkout in Unity:

```text
python Tools/build_tactical_math.py --build-directory .native-math-build --target win-x64
python3 Tools/build_tactical_math.py --build-directory .native-math-build --target linux-x64
python3 Tools/build_tactical_math.py --build-directory .native-math-build --target osx
```

Choose the command matching the host operating system. macOS produces a universal Intel/Apple
Silicon library. `build.sh test`, `build.sh coverage`, and `build.sh build` prepare the host library
automatically. Native CTest contracts run before installation; any compilation, load, ABI, or
contract failure stops the build. Generated libraries and their deterministic import metadata live
under `Assets/Plugins/TacticalMath` and are ignored by Git. Do not edit generated import settings.

The reusable `Native vector math` workflow builds and tests on Windows, Linux, and Apple Silicon,
then loads that same universal macOS artifact on an Intel runner. It also supports manual dispatch
without media assets or Unity. The Unity workflow downloads all three verified native artifacts
before tests or player builds. Cross-building a player locally requires the corresponding native
artifact as well as the host library. Desktop packaging rejects a player missing its native vector
library or containing the wrong architecture.

Native calls establish round-to-nearest arithmetic with gradual float underflow and restore the
caller's floating-point controls. Ambiguous length rounding and nonfinite inputs execute the target
kernel directly. NaN payload/sign choices depend on the target arithmetic; equal-payload,
opposite-sign ties have no universal sign contract.
