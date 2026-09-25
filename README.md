# Rebellion 2

An open-source remake of the 1998 strategy game *Star Wars: Rebellion*, built with Unity.
Despite the name, Rebellion 2 is not a sequel: it is a modernized recreation of the original game
with the improvements we have always wanted.

<img width="3770" height="2110" alt="Rebellion 2 strategy view" src="https://github.com/user-attachments/assets/f3b454e3-aa88-4363-b8ba-19d2447acb2e" />

## Download

Rebellion 2 is available in early access for Windows and macOS.

<table>
  <tr>
    <td align="center" width="50%">
      <a href="https://github.com/adasgames/rebellion2-installers/releases/latest/download/Rebellion2-Windows-Setup.exe">
        <img src="Docs/Assets/windows.svg" width="64" alt="Windows"><br>
        <strong>Install latest for Windows</strong>
      </a><br>
      <sub>64-bit Windows installer</sub>
    </td>
    <td align="center" width="50%">
      <a href="https://github.com/adasgames/rebellion2-installers/releases/download/latest-macos/Rebellion2-macOS.zip">
        <img src="Docs/Assets/apple.svg" width="64" alt="macOS"><br>
        <strong>Download latest for macOS</strong>
      </a><br>
      <sub>Universal Intel + Apple Silicon app</sub>
    </td>
  </tr>
</table>

[View release notes and all downloads](https://github.com/adasgames/rebellion2-installers/releases/latest)
or browse the public [installer and launcher source code](https://github.com/adasgames/rebellion2-installers).

The installer verifies ownership automatically. You must own either *Star Wars: Rebellion* or
*Star Wars: Empire at War: Gold Pack* on GOG or Steam.

> [!IMPORTANT]
> Rebellion 2 remains in active development. Save compatibility is not guaranteed between releases.

## Project status

Rebellion 2 is approximately **60% complete toward a feature-complete single-player campaign**.

| Area | Estimate | Area | Estimate |
| --- | ---: | --- | ---: |
| Foundation and Data | 60% | Strategy Simulation | 60% |
| Strategic AI | 50% | Missions | 80% |
| Original Game Events | 85% | Custom Events API | 30% |
| Strategy Interface | 80% | UI Upscaling | 40% |
| Save Games | 100% | Settings | 10% |
| Moddability | 65% | Modding Tools | 0% |
| Tactical Simulation | 0% | Tactical AI | 0% |
| Multiplayer | 0% | | |

## Documentation

- [Development setup and commands](Docs/Development.md)
- [Modding and content packs](Docs/Modding/Index.md)
- [Creating game events](Docs/Modding/Events/Index.md)

Game assets and generated UI artifacts live outside this source repository. Development checkouts
use the separate `rebellion2-media` repository and the ignored asset directories described in the
[development guide](Docs/Development.md). CI installs its media checkout automatically.

## Contributing and reporting bugs

Focused fixes and improvements are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening
a pull request.

Before reporting a bug, search the [existing issues](https://github.com/davidadas/rebellion2/issues).
Include your platform, game version, reproduction steps, expected and actual behavior, and relevant
logs or screenshots. Do not attach copyrighted game assets or secrets.

## Legal

This unofficial fan project is not affiliated with or endorsed by Disney, Lucasfilm, or the owners
of *Star Wars*. Copyrighted game assets are not distributed in this repository and must not be
redistributed by players, modders, or contributors. Installed game data and copyrighted assets must
not be redistributed, uploaded, or shared.

Original source code authored by David Adams for this project is available under the
[PolyForm Noncommercial License 1.0.0](LICENSE.md). It may be used, modified, and redistributed for
permitted noncommercial purposes.

The license does not cover content or assets, including images, icons, artwork, 3D models, textures,
animations, audio, video, fonts, game data, other media, third-party software, names, or trademarks.
Those materials remain subject to their respective rights and licenses.
