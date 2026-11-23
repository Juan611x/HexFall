# HexFall license notice and Unity linking permission

Copyright (C) 2026 Juan611x

HexFall is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3 of the License. The full text is in [LICENSE](LICENSE).

HexFall is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

## Additional permission under GNU GPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with the Unity engine and its runtime libraries published by Unity Technologies (or a modified version of that engine or those libraries), containing parts covered by the terms of the Unity Terms of Service, the licensors of this Program grant you additional permission to convey the resulting work.

### Why this permission exists

The game client is a Unity project. Building it combines the HexFall code with the Unity engine runtime, which is proprietary and is not a "System Library" in the sense of the GPL. Without this permission, the built game (the WebGL build published in GitHub Releases and on the hosted site) could not be conveyed under the GPL. The permission covers only that combination; everything else in the GPL applies unchanged, including the obligation to provide the Corresponding Source of the HexFall code itself.

## What this license covers

| Covered by GPL-3.0 with the permission above | Not covered: keeps its own license |
|---|---|
| All code in this repository: the Unity C# scripts and tests (`game/Assets/_Project/`), Terraform (`infra/`), Python services and tools (`services/`, `tools/`), workflows (`.github/`) | Third-party assets listed in [docs/ASSET_CREDITS.md](docs/ASSET_CREDITS.md), such as the Quaternius wizard, Poly Haven textures, Kenney and OpenGameArt sounds (CC0 1.0) and the Cinzel and Pixelify Sans fonts (SIL OFL 1.1). Their license files sit next to them. |
| Self-made content listed as such in `docs/ASSET_CREDITS.md` (for example the menu theme, the generated arena, the logo artwork) and the documentation | The Unity engine, its packages and any template content Unity generates, under Unity's own terms |
