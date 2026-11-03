# Lexer's King's Field III fork

This fork targets Japanese King's Field III, the US King's Field II disc
SLUS-00255. The baseline is Voicedrew11/verdite3 commit
`ebc307bc42669519af736c6d29cd5d1cba06881a`. No Verdite2 addresses or generated
code belong here. The launcher identifies the game by Japanese numbering and
checks Lexer-Lux/verdite3 for releases. Disc bytes and generated game sources
remain private local build inputs.

## Requested work

"adding in that separate view distance settings things" means a numeric view
distance independent of the smooth distance-fade on/off choice. The requested
Classic/Modern control choice belongs in first-run and rerun setup. Forget Me Not
is a separate ordinary user-library source mod for containers and real doors;
it must never reveal illusory walls.

The additional requested mods are Better Sprint, Better Poison and XP Bar. Each
is an ordinary source-only user-library mod, separate from the fork's default
gameplay. All four requested mods are imported into the library, disabled.

- [ ] Deliver and verify the native fork without interrupting an active game.
- [ ] Integrate shared Lexeditor Mods and Settings.
- [ ] Verify numeric view distance and smooth fade independently, including persistence.
- [ ] Offer the actual upstream Classic controls and the requested Modern controls,
  preserving custom bindings and all menu/item/map actions.
- [ ] Implement Forget Me Not with verified KF3 interaction types and save identity.
- [ ] Check rendered controls and GPU gameplay; source tests do not establish appearance.

## Distance settings

KF3 already has two independent renderer properties. Its in-game menu previously
combined them into one Original/Enhanced row. Keep their actual native keys:

| Property | Key | Default | Range |
|---|---|---|---|
| View distance | `kf3.renderdistance` | 16 | 0, or 1..30 tiles |
| Fade band | `kf3.renderdistance.fade` | 3 | 0 off, or 0.25..8 tiles |
| Enhancement distance | `kf3.enhancedistance` | 0 everywhere | Graphics effects only |
| Atmospheric fog | `kf3.fog` | 2 | Corners, pixel depth, eye distance |

The smooth-fade checkbox is a semantic view of the existing fade band, not a
second saved key. Off stores zero. On keeps an existing positive band or uses
the native default of three. Neither operation writes view distance.

The view slider spans the native accepted range. Zero retains the game's changing
draw radius; smaller positive values cannot reduce that radius. The maximum of
30 is retained. The renderer also fades view depth before 65,024 world units
(about 31.75 tiles) to protect its 16-bit depth representation. Changing this
limit requires renderer work, not merely increasing a slider.

Distance fade uses the existing retained GPU path for main-view geometry and
models. The sky and arm are excluded. The packet renderer and reflections keep
the game's reach. Creatures retain their own activation/spawn-distance limits.
Fog can still obscure geometry, independent of how far it is submitted.

## Modern menu choice

Lexer explicitly chose physical Triangle for inventory and Options/Start for the
separate card/options menu. Physical Circle is held to run during play. Classic
must be derived from the unmodified baseline port, including its menu actions.

## Safety and verification

Builds, generated sources and isolated test data live under the session's temporary
directory. Do not modify original game executables, current saves or any active
installation while it is running. New native settings and source mods need
separate build, storage, rendered-UI and real-game evidence before completion.

### Observed evidence

- All native sources compile against the private SLUS-00255 recompilation.
- The focused native check covers actual Classic/Modern tables, preserving Custom,
  persistent bindings, saved SDK pad callers, menu bridges, independent distance
  edits, save/discard and menu capacity.
- First-run and rerun controls were rendered offscreen from the actual ImGui
  widgets and inspected at small windows and increased scale.
- A private headless New Game completed its opening cinematic. Equal two-second
  input segments measured walk 5,730 units, Run 9,606 and Interact 5,730. Native
  walk limit was 200, 336 and 200 respectively; release returned it to 200. This
  measures the native run path, not physical controller feel.
- Forget Me Not compiled through the real native source-mod loader, with 19 hooks.
  A second private launch consumed a deployment produced by Lexeditor's actual
  adapter, confirming its `interface.ini` flags, rather than a parallel test format.
- Runtime shaders pass extraction/static checks and official glslang compilation
  and linking: 22 stages, 15 programs, no findings. This does not establish scene
  appearance.
- Player card/settings hashes were unchanged across the private runs. No active
  game was stopped. The installation below was completed separately; player
  files were left unchanged.
- XP Bar's focused source check compiles its actual source and the generic
  physical-framebuffer HUD API. Synthetic checks cover native progression,
  full-width bottom placement at several framebuffer sizes, GL state restoration,
  clipping, callback disposal and overlay transitions. They do not establish
  rendered gameplay appearance. The production Lexeditor adapter and real native
  compiler/loader subsequently loaded all four source mods together.

### Shared components and delivery

The shared onboarding source is checkpointed in Lexer-Lux/verdite-core on
`codex/kf3-binding-setup` (`15785d3`). The generic map-half glow, physical HUD,
headless diagnostics, dependency-aware source-mod priority and compiler metadata
are checkpointed in Lexer-Lux/verdite-recompone on `codex/kf3-mod-compiler`
(`b52b67c`). Neither change
was submitted upstream or pushed to a default branch.

The normal Windows fork is installed at `D:/Programs/Verdite3`. The original
installation is retained at `D:/Programs/Verdite3.original`; player files were
left unchanged. The latest candidate's414 installed files were hash-verified.
Its normal installed launcher privately compiled the shipped sources from the
disc and loaded a real source mod before reaching the game overlay, using
isolated data. The windowless launcher bypasses invisible setup/popups and reports
build failures through its log. GPU scene appearance and physical-pad
feel are still unverified. The distribution contains the launcher/runtime and
source build inputs, without disc data, card files or the generated game assembly.

The requested Forget Me Not source mod is in the user's library, disabled. It is
not bundled into the fork as gameplay behavior. Its own README and credits record
the independently verified KF3 interaction facts and the material reference
AnatoliySolomichev/kings_field_2_re. Its read-only disc census covers the container
and real-door families, excludes hidden-wall/transition opcodes, and proves the
native lift-door glow footprints do not intersect hidden-wall cell/halves.

Better Sprint, Better Poison and XP Bar are also disabled source-only library
mods. Their focused checks and successful native-loader attachment remain
separate from visual acceptance. No mod's gameplay behavior is bundled into the
fork. No user game was stopped or restarted during installation or diagnostics.
