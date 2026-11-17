# Menu audio

Sounds are wired in `MenuBootstrap` (composition root) and driven by the same pointer-down hook as the wizard cast (see `RENDERING.md`).

| Event | Trigger | Sound |
| --- | --- | --- |
| Hover | `PointerEnterEvent` (trickle-down on the panel root) whose target is an enabled `Button` | `hover` |
| Press | `PointerDownEvent` / `NavigationSubmitEvent` on an enabled `Button` | `click`, or `back` for `back-button` |
| Cast | Every wizard `Cast()` (`SpellSoundWizardAnimator` decorates `IWizardAnimator`) | random `spellClips` entry, never the same twice in a row |

Hover only counts the `Button` itself: its children raise their own enter events while the pointer is already inside, so they are ignored.

## Swapping sounds

Edit `Assets/_Project/Audio/MenuAudioLibrary.asset` in the Inspector: clip and volume per slot, plus the spell clip list and volume. No code change needed. Any number of spell clips works.

Import settings used: UI `.ogg` mono, Decompress On Load, Vorbis 70%; spell `.wav` mono, Compressed In Memory, Vorbis 70%.

## Scene

- `MenuUI` has the UI `AudioSource` (2D).
- The wizard root (the object with the `Animator`, not its FBX child of the same name) has the spell `AudioSource` (2D).
- `MenuCamera` keeps the `AudioListener`.

## Web

Browsers block audio until the first user gesture, so hover sounds before the first click are silent in WebGL.
