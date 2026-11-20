# Asset Credits

Every third-party asset used by HexFall (in the game, mockups, or docs) is listed here with its source, author, and license. Add an entry **before** an asset enters the repository or a published mockup.

**Rule:** only CC0, public domain, or permissive licenses (SIL OFL, MIT, CC-BY with attribution recorded here). No "free for personal use" assets.

| Asset | Used for | Author | License | Source |
|---|---|---|---|---|
| RPG Characters pack (Nov 2020), `Wizard` model and textures | Menu wizard in Unity (`game/Assets/ThirdParty/Quaternius/Wizard/`), Blender source (`art/quaternius/`), and the mockup renders; candidate player character | Quaternius | CC0 1.0 | [OpenGameArt: Lowpoly RPG Characters](https://opengameart.org/content/lowpoly-rpg-characters) · [Direct download (zip)](https://opengameart.org/sites/default/files/rpg_characters_-_nov_2020.zip) · [quaternius.com](https://quaternius.com) |
| Universal Animation Library (Standard), `UAL1_Standard.fbx` (no root motion) | Third-person character animations, retargeted as Humanoid (`game/Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/`) | Quaternius | CC0 1.0 | [quaternius.com](https://quaternius.com) · [Animation viewer](https://quaternius.com/animviewer.html) |
| Darkness_Castle_Temple, image `111_3.png` | Menu background (moonlit stone temple) | dreamsearchrepeat | CC0 1.0 | [OpenGameArt: Darkness_Castle_Temple](https://opengameart.org/node/175643) · [Image](https://opengameart.org/sites/default/files/111_3.png) |
| Stone Tiles 02 (diffuse, 1k) | Menu arena floor (as `T_StoneTiles.png`, see derived assets) | Charlotte Baglioni | CC0 1.0 | [Poly Haven: Stone Tiles 02](https://polyhaven.com/a/stone_tiles_02) · [License](https://polyhaven.com/license) |
| Castle Brick Broken 06 (diffuse, 1k) | Menu arena pillars, step and rubble (as `T_CastleBrick.png`, see derived assets) | Rob Tuytel | CC0 1.0 | [Poly Haven: Castle Brick Broken 06](https://polyhaven.com/a/castle_brick_broken_06) · [License](https://polyhaven.com/license) |
| Cinzel Decorative (Black, Bold) | Game logo and screen titles (mockups and Unity UI, `game/Assets/_Project/UI/Fonts/`) | Natanael Gama | SIL OFL 1.1 | [Google Fonts](https://fonts.google.com/specimen/Cinzel+Decorative) · [License](https://github.com/google/fonts/blob/main/ofl/cinzeldecorative/OFL.txt) |
| Cinzel | Screen titles in the HTML mockups only (not shipped in Unity) | The Cinzel Project Authors | SIL OFL 1.1 | [Google Fonts](https://fonts.google.com/specimen/Cinzel) · [License](https://github.com/google/fonts/blob/main/ofl/cinzel/OFL.txt) |
| Interface Sounds 1.0: `tick_001.ogg`, `select_002.ogg`, `back_001.ogg` | Menu button hover, click and back sounds (`game/Assets/ThirdParty/Kenney/InterfaceSounds/`) | Kenney | CC0 1.0 | [kenney.nl: Interface Sounds](https://kenney.nl/assets/interface-sounds) · [OpenGameArt mirror (zip)](https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip) |
| Basic Spell Impacts: `Ice Spell Impact 1–3` (renamed `IceSpellImpact1–3.wav`) | Wizard spell cast sound in the menu, one picked at random per cast (`game/Assets/ThirdParty/CC0Combat150/MagicSpells/`) | lentikula (source `s06`), via the "150 Combat Sound Effects" compilation by untamedpixels | CC0 1.0 | [itch.io: Basic Spell Impacts](https://lentikula.itch.io/freecc0-basic-spell-impacts-sfx) · [OpenGameArt: 150 Combat Sound Effects](https://opengameart.org/node/195932) · per-source licenses in `LICENSES.txt` next to the files |
| Cathedral in the forest (ambient loop), `forest_2.ogg` (stored as `cathedral_forest.ogg`) | Ambient layer under the menu theme (`game/Assets/ThirdParty/OpenGameArt/CathedralInTheForest/`) | congusbongus | CC0 1.0 | [OpenGameArt: Cathedral in the forest](https://opengameart.org/node/143245) · [File](https://opengameart.org/sites/default/files/forest_2.ogg) |
| Pixelify Sans (Regular, Bold) | UI text, buttons, inputs (mockups and Unity UI; static TTFs in `game/Assets/_Project/UI/Fonts/`) | The Pixelify Sans Project Authors | SIL OFL 1.1 | [Google Fonts](https://fonts.google.com/specimen/Pixelify+Sans) · [Static TTFs: eifetx/Pixelify-Sans](https://github.com/eifetx/Pixelify-Sans/tree/main/fonts/ttf) · [License](https://github.com/google/fonts/blob/main/ofl/pixelifysans/OFL.txt) |

## Derived assets

Assets we produced from the sources above. CC0 allows modification without attribution, but the chain is recorded for traceability.

| Derived asset | Made from | How |
|---|---|---|
| `wizard_cast.png` (240×300) | Quaternius `Wizard.blend`, action `Spell1`, frame 14 | Blender 4.5 headless render: EEVEE, nearest-neighbour texture sampling, warm key + violet rim light, transparent background |
| `wizard_idle.png` (200×300) | Quaternius `Wizard.blend`, action `Idle_Weapon`, frame 20 | Same render setup as above |
| `WizardHumanoid.fbx` (`game/Assets/_Project/Art/Characters/Wizard/`) | Quaternius `Wizard.blend` | Built by `art/quaternius/build_wizard_humanoid.py`: leg IK constraints removed, `Foot.L/R` re-parented under `LowerLeg.L/R` so Unity can build a Humanoid avatar; mesh and armature only, no animation. Materials remapped to the original `M_Wizard`/`M_WizardStaff` |
| `T_StoneTiles.png`, `T_CastleBrick.png` (128×128; in `art/environment/textures/` and `game/Assets/_Project/Art/Environment/Textures/`) | Poly Haven diffuse maps above | Downscaled by `art/environment/build_menu_arena.py`; point-filtered in Unity, tinted per material |
| `MenuArena.fbx` / `MenuArena.blend` | Self-made geometry + the two textures above | Generated by `art/environment/build_menu_arena.py` (hex platform, step, rune rings, 6 pillars, rubble) |
| `bg_temple_480.png` (480×270) | dreamsearchrepeat `111_3.png` (1920×1080) | Downscaled to 480×270; upscaled in the UI with `image-rendering: pixelated` and tinted violet for the PSX look |
| HexFall logo "Fused HF" (`art/logo/hexfall_logo.svg`; exports `T_LogoEmblem.png`, `T_AppIcon.png`, WebGL `favicon.ico`) | Cinzel Decorative Black glyphs `H` and `F` (SIL OFL 1.1) | Glyph outlines extracted with fontTools by `art/logo/build_fused_hf.py`, fused on a shared stretched stem, F serif hand-edited, placed in a self-made hexagon; rendered by `art/logo/export_logo.py`. The OFL permits this artwork use; the font itself ships unmodified with `OFL-CinzelDecorative.txt` |

## Self-made assets

Created for HexFall with no third-party source; listed so every binary in the repository is accounted for.

| Asset | Location | How |
|---|---|---|
| `T_LightShaft.png` (64×256), `T_Moon.png` (64×64) | `game/Assets/_Project/Art/Environment/Textures/` | Generated procedurally in the Unity Editor (tapered alpha gradient for the moonbeam, crescent mask for the moon) |
| `MenuTheme.wav` (91.4 s seamless loop, 22.05 kHz mono) | `game/Assets/_Project/Audio/Music/` | Composed and synthesized by `art/audio/compose_menu_theme.py` (pure Python, chip-style voices, D Phrygian dominant, 84 BPM); key chosen to match the Cathedral ambient layer (D/A drone) |
| `Scanlines.png` (1×3) | `game/Assets/_Project/UI/Textures/` | Generated for HexFall: tiling pattern for the UI scanline overlay |
| Hexagon emblem and UI icons (eye, dice, envelope, gear) | `HexagonElement.cs`, `IconElement.cs` | Drawn at runtime with UI Toolkit `Painter2D`; no image files |

## Unity packages

Code packages from Unity Technologies, declared in `game/Packages/manifest.json` and downloaded by the Package Manager (never committed; `Library/` is ignored). Listed so every third-party dependency is in one place. Packages that ship with the project template (URP, Input System, Test Framework, UI Toolkit) are not repeated here.

| Package | Version | Used for | License |
|---|---|---|---|
| Cinemachine (`com.unity.cinemachine`) | 3.1.7 | Third-person camera: follow, shoulder aim, wall collision | [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) |
| Animation Rigging (`com.unity.animation.rigging`) | 1.4.1 | Runtime IK: head and torso aim at the crosshair, hand locked to the staff | [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) |
| Splines (`com.unity.splines`) | 2.9.0 | Not used directly; pulled in as a dependency of Cinemachine | [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) |

## Required local import: Starter Assets

The `PlayerWizard` prefab and the `Sandbox` scene use Unity's free **First Person + Third Person | Character Controllers** package (Starter Assets 2.0.2, Unity Technologies, Asset Store). Its Asset Store licence is still under review, so it is not in this repository (`game/Assets/Starter Assets/` is ignored). Before opening `Sandbox`, add the package to your Unity account and import it from **Window > Package Manager > My Assets** into `game/Assets/Starter Assets/`. Without it the player shows missing scripts and the scene has no camera rig; the main menu does not depend on it.

## Engine template content

| Asset | Location | Note |
|---|---|---|
| `URP.png` and the `TutorialInfo` scripts | `game/Assets/TutorialInfo/` | Created automatically by Unity's Universal 3D project template; not used by HexFall and safe to delete |

## Evaluated, not used

| Asset | Author | License | Reason not used |
|---|---|---|---|
| [Background Nightsky](https://opengameart.org/node/126300) | tigitalart | CC0 1.0 | Soft cartoon style clashes with the PSX look |
| [Fantasy Concept 3](https://opengameart.org/node/158980) | drummyfish | CC0 1.0 | Bright daytime palette; kept as a reference for a dithered 320×240 style |
