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

The 2026-11-03 visibility investigation found the installed retained renderer
already submitted terrain beyond15 tiles, but the authored atmospheric cue made
it black. Paired GPU snapshots at one camera recorded7,735 terrain pixels beyond
15 tiles with no visible colour; bypassing only the GPU cue lit6,844 of them.
The15-tile comparison contained only130 such pixels. This is diagnostic evidence,
not an installed fix claim.

The correction scales only the current retained main view's fog sampling depth
by `max(1, requested tiles / native radius)`. It leaves authored near/far words,
the disabled-fog sentinel, geometry coordinates, depth testing and distance fade
unchanged. Native reach and packet/reflection fog keep scale1. The existing
30-tile maximum and65,024-unit depth guard remain. Native controls/distance tests,
runtime compilation and22 shader stages/15 links pass; the new shipped build
still needs matched-camera, restart and rendered-UI verification.

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
`codex/kf3-binding-setup` (`cf77dff`). The generic map-half glow, physical HUD,
headless diagnostics, dependency-aware source-mod priority and compiler metadata
and retained fog-depth scaling are checkpointed in Lexer-Lux/verdite-recompone on
`codex/kf3-fog-range` (`f53d359`). Neither change
was submitted upstream or pushed to a default branch.

The normal Windows fork is installed at `D:/Programs/Verdite3`. The original
installation is retained at `D:/Programs/Verdite3.original`; player files were
left unchanged. The latest candidate's414 installed files were hash-verified.
Its normal installed launcher privately compiled the shipped sources from the
disc and loaded a real source mod before reaching the game overlay, using
isolated data. The windowless launcher bypasses invisible setup/popups and reports
build failures through its log. Extended distance and independent fade were
subsequently verified on the installed build's actual GPU output as described
below; physical-pad feel and the mods' appearance remain separate checks.
The distribution contains the launcher/runtime and
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

### Installed regression checks, 2026-11-03

The normal installed launcher built the shipped `04b43f8` source into a private
data/cache folder and rendered through NVIDIA OpenGL 4.5. At a fixed camera,
distance 15/fade 0 had no lit geometry beyond 15 tiles; distance 30/fade 0 had
4,367 lit far pixels, and distance 30/fade 8 had 4,204. Main and presented depth
captures matched the same retained serial, with no far geometry erased at
presentation. The final framebuffer images were inspected: farther walls and
trees appeared, and toggling fade changed the edge while distance stayed 30.
No diagnostic fog bypass was active. After a process restart, saved distance 24
and fade 2.5 loaded unchanged, with `rdFogScale=24/13`; 2,735 lit pixels remained
beyond 15 tiles in both matched main/present captures.

Actual installed launcher first-run and compiled-game rerun widgets both show
Controls at step 2 of six. The rerun retains a custom binding as Keep as is.
Actual compiled-game settings render 30.0 with fade off and with band 8.00; the
native menu formats the maximum step as 30. Lexeditor's real number input has
min 0/max 30/step 0.1, and a private UI edit/save/reload retained 30 through
independent fade off/on changes. Large and small rendered screens were viewed.

The installed game's actual binding/SDK path, supplied private synthetic raw
input, opened Earth Herb's native Guide with Modern Triangle and keyboard Q.
Holding/repressing Triangle did not dismiss it; Circle returned to the item list
and then gameplay. Classic Triangle also opened Guide and Circle returned to
the list. These are isolated input-path checks, not a physical controller test.

The initial hidden GPU test mistakenly opened audio and was stopped. Later
tests intercept Audio.Initialize before startup and assert no audio context or
mixer thread, in addition to zero private volume and disabled rumble. Owned test
processes exited and player cards/settings hashes remained unchanged.
