# Building and testing the WebGL build

## Prerequisites

- Unity `6000.5.3f1` with the **WebGL Build Support** module (Unity Hub).
- Python 3 (local server only), Windows PowerShell 5.1+.
- The Unity editor **closed** for this project (batch mode needs exclusive access).

## Build

From the repo root:

```powershell
powershell -File tools/build-webgl.ps1            # output: game/Builds/WebGL
powershell -File tools/build-webgl.ps1 -OutputPath Builds/Custom
```

- Takes about 6 minutes. The log is `game/Builds/webgl_build.log`.
- The script prints one result line and exits with Unity's exit code (0 = success):
  `HEXFALL_BUILD_RESULT Succeeded errors=0 size=<bytes> output=<path>`
- Scenes come from **File > Build Profiles** (enabled scenes only).
- Set `UNITY_EDITOR` to a `Unity.exe` path if the editor is not in the default Hub location.

Output layout (git-ignored):

```
game/Builds/WebGL/
  index.html        custom template (Assets/WebGLTemplates/HexFall)
  Build/            *.wasm.br, *.data.br, *.framework.js.br, *.loader.js
```

## Serve locally

```powershell
python tools/serve_webgl.py            # http://127.0.0.1:8765/
python tools/serve_webgl.py --dir game/Builds/WebGL --port 9000
```

Why not a plain static server: Unity emits Brotli files (`.br`). Browsers need
`Content-Encoding: br` plus the real MIME type, or loading fails. The script sets:

| File | Content-Type |
|------|--------------|
| `*.wasm.br` | `application/wasm` |
| `*.framework.js.br` | `application/javascript` |
| `*.data.br` | `application/octet-stream` |

It also disables caching and binds to `127.0.0.1` only.

## Expected behavior

- **FSR warning (benign):** the browser console shows
  `Shader 'Hidden/Universal Render Pipeline/Edge Adaptive Spatial Upsampling' is not supported (in 'Blit FSR Upscaling')`.
  URP builds every post-process pass, and EASU needs Shader Model 4.5, which WebGL 2 lacks.
  Only the FSR pass disables itself. The project uses Nearest-Neighbor upscaling at render
  scale 0.5, so FSR is never used. Do not switch to FSR for web.
- **Audio starts after the first click/tap.** Browsers block audio before a user gesture
  (see [AUDIO.md](AUDIO.md)).

## Build side effects

A batch build re-serializes tracked files and creates a folder. **Do not commit them.**
After a build, restore (a batch EditMode test run was observed not to touch them):

```bash
git restore game/Assets/Settings game/ProjectSettings/ProjectSettings.asset
rm -rf game/Data
```

Touched: `Assets/Settings/*_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`,
`DefaultVolumeProfile.asset`, `ProjectSettings/ProjectSettings.asset`, and `game/Data/Plugins`
(Burst libraries).

## Production hosting

Static hosting (for example S3 + CloudFront) must serve the same metadata as the local server:
`Content-Encoding: br` and the MIME types above on the `.br` files. Without them the
game does not load.

## Alternative: from the editor

- Menu **HexFall > Build > WebGL** builds to `Builds/WebGL` using the same code path.
- Or **File > Build Profiles > WebGL > Build And Run**, which serves the build with Unity's
  own local server (not yet tried on this project).
